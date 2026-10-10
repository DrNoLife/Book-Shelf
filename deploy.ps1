<#
.SYNOPSIS
  One-command deploy of LnShelf to the local server.

.DESCRIPTION
  Builds both Docker images locally (fast, layer-cached), ships them to the server over the LAN in
  one tarball, and restarts the stack there — no source upload and no .NET build on the server. The
  server keeps its own docker-compose.override.yml (credentials, anything host-specific); only the
  base docker-compose.yml and the images are sent.

  Two images, because the client and the API are separate deployables (feature-spec §2): nginx
  serves the WASM bundle and proxies /api/* to the API container. Only the client publishes a host
  port — 8091, since the same server's Hearth stack already owns 8090, 5432, 9000, 9001 and 6379.

  Prerequisites (one-time):
    - Docker Desktop running locally.
    - The Six Labors license key in the SixLaborsLicenseKey environment variable (user scope is
      fine); ImageSharp 4 will not compile without it. See README.md, Deploy.
    - SSH key auth to the server (key at $Key).
    - The server address and account, as parameters or in LNSHELF_DEPLOY_SERVER and
      LNSHELF_DEPLOY_USER. They are not written here, so the script holds nothing about
      one particular network.
    - A .env next to the compose file on the server with the credentials
      docker-compose.production.yml asks for (see README.md, Deploy).

.EXAMPLE
  $env:LNSHELF_DEPLOY_SERVER = '192.0.2.10'; $env:LNSHELF_DEPLOY_USER = 'deploy'; ./deploy.ps1

.EXAMPLE
  ./deploy.ps1 -Server 192.0.2.10 -User deploy
#>
[CmdletBinding()]
param(
    [string]$Server    = $env:LNSHELF_DEPLOY_SERVER,
    [string]$User      = $env:LNSHELF_DEPLOY_USER,

    # Under the account's home unless given, so it follows whatever $User is.
    [string]$RemoteDir = $(if ($env:LNSHELF_DEPLOY_DIR) { $env:LNSHELF_DEPLOY_DIR } else { 'docker/lnshelf' }),

    # A project-specific key if one has been made, otherwise the server's existing Hearth key —
    # same host, same account, so it already grants what this needs.
    [string]$Key = $(
        $projectKey = "$env:USERPROFILE\.ssh\id_lnshelf"
        if (Test-Path $projectKey) { $projectKey } else { "$env:USERPROFILE\.ssh\id_hearth" }
    ),

    # Where the app is reached. With login on, that is the HTTPS hostname registered with
    # the provider; the default is the client's published port, which proves the stack is
    # up but cannot complete a login.
    [string]$Url       = $env:LNSHELF_DEPLOY_URL,
    [string]$ApiTag    = 'lnshelf-api:latest',
    [string]$ClientTag = 'lnshelf-client:latest'
)

$ErrorActionPreference = 'Stop'

if (-not $Server -or -not $User) {
    throw 'Say where to deploy: -Server and -User, or LNSHELF_DEPLOY_SERVER and LNSHELF_DEPLOY_USER.'
}
if (-not $Url) { $Url = "http://${Server}:8091" }
Set-Location -Path $PSScriptRoot   # repo root (both Dockerfiles + the compose file live here)

$target  = "$User@$Server"
$sshOpts = @('-i', $Key, '-o', 'StrictHostKeyChecking=accept-new', '-o', 'ConnectTimeout=10')

# Every remote command sees both compose files and placeholder digests (see the upload step).
$composeEnvironment = 'export PATH="$PATH:/snap/bin" COMPOSE_FILE=docker-compose.yml:docker-compose.local.yml LNSHELF_API_DIGEST=local-build LNSHELF_CLIENT_DIGEST=local-build; '

function Invoke-Remote([string]$Command) {
    & ssh @sshOpts $target ($composeEnvironment + $Command)
    if ($LASTEXITCODE -ne 0) { throw "Remote command failed (exit $LASTEXITCODE): $Command" }
}

# Polls until the endpoint answers 200 or the attempts run out. Startup is not instant: migrations
# apply on boot, and nginx has to see the API resolve.
function Wait-ForHttpOk([string]$Uri, [int]$Attempts = 20, [int]$DelaySeconds = 3) {
    foreach ($attempt in 1..$Attempts) {
        try {
            $response = Invoke-WebRequest -Uri $Uri -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -eq 200) { return $true }
        }
        catch {
            # Connection refused / 502 while the stack comes up. Keep waiting.
        }
        Start-Sleep -Seconds $DelaySeconds
    }
    return $false
}

# ImageSharp 4 needs a Six Labors license key to compile. Look in the user scope as well:
# a variable set with setx or System Properties is invisible to a shell opened before it was set.
if (-not $env:SixLaborsLicenseKey) {
    $env:SixLaborsLicenseKey = [Environment]::GetEnvironmentVariable('SixLaborsLicenseKey', 'User')
}
if (-not $env:SixLaborsLicenseKey) {
    throw 'SixLaborsLicenseKey is not set (checked the process and the user environment). ImageSharp 4 needs a Six Labors license key to build; see README.md, Deploy.'
}

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

Write-Host '==> Building images locally…' -ForegroundColor Cyan
# The key goes in as a BuildKit secret read from the environment variable (never --build-arg,
# which would be recorded in the image history).
& docker build --secret id=sixlabors_license,env=SixLaborsLicenseKey -t $ApiTag -f Dockerfile.api .
if ($LASTEXITCODE -ne 0) { throw 'docker build (api) failed.' }
& docker build -t $ClientTag -f Dockerfile.client .
if ($LASTEXITCODE -ne 0) { throw 'docker build (client) failed.' }

# One tarball for both images: they share the same base layers, so saving them together ships those
# layers once instead of twice.
$tar = Join-Path $env:TEMP 'lnshelf-img.tar'
Write-Host '==> Saving images to a tarball…' -ForegroundColor Cyan
& docker save $ApiTag $ClientTag -o $tar
if ($LASTEXITCODE -ne 0) { throw 'docker save failed.' }
$sizeMb = [math]::Round((Get-Item $tar).Length / 1MB, 1)
Write-Host "    image tarball: $sizeMb MB"

try {
    Write-Host '==> Uploading images + compose to server…' -ForegroundColor Cyan
    Invoke-Remote "mkdir -p '$RemoteDir'"
    & scp @sshOpts $tar "${target}:$RemoteDir/lnshelf-img.tar"
    if ($LASTEXITCODE -ne 0) { throw 'scp (images) failed.' }
    # The server runs it as the plain docker-compose.yml of that directory, so its own
    # docker-compose.override.yml layers on top without any extra -f arguments.
    & scp @sshOpts 'docker-compose.production.yml' "${target}:$RemoteDir/docker-compose.yml"
    if ($LASTEXITCODE -ne 0) { throw 'scp (compose) failed.' }

    # The compose file pulls images from GHCR by digest (the tag-driven deploy, ops/deploy).
    # Until that path is live this script still ships locally built images, so a second file
    # puts the local tags back, and every compose call below reads both through COMPOSE_FILE.
    # The digests only have to be set for interpolation; the local file replaces the images.
    $localOverride = "services:`n  api:`n    image: $ApiTag`n  client:`n    image: $ClientTag`n"
    $localOverride | & ssh @sshOpts $target "cat > '$RemoteDir/docker-compose.local.yml'"
    if ($LASTEXITCODE -ne 0) { throw 'Writing the local-images override failed.' }

    # Migrations apply on boot and cannot be rolled back, so take a dump first, while the old
    # API still owns the schema. Custom format (-Fc) is compressed and restores with pg_restore.
    # Skipped only when postgres is not running yet (the very first deploy); a failed dump of
    # a running database stops the deploy. The last five are kept.
    Write-Host '==> Backing up the database before migrating…' -ForegroundColor Cyan
    $dumpName = "pre-deploy-$(Get-Date -Format 'yyyyMMdd-HHmmss').dump"
    Invoke-Remote ("cd '$RemoteDir' && mkdir -p backups && " +
        "if [ -n `"`$(docker compose ps --status running -q postgres 2>/dev/null)`" ]; then " +
        "docker compose exec -T postgres pg_dump -U lnshelf -d lnshelf -Fc > backups/$dumpName && " +
        "ls -1t backups/pre-deploy-*.dump | tail -n +6 | xargs -r rm -f && echo `"    backup: backups/$dumpName`"; " +
        "else echo '    postgres is not running yet; nothing to back up'; fi")

    # Keep the images being replaced under a :previous tag, so the prune below cannot delete
    # them and a bad release can be rolled back by retagging (README.md, Deploy).
    $previousTags = foreach ($tag in $ApiTag, $ClientTag) {
        $previous = $tag -replace ':[^:/]+$', ':previous'
        "{ docker image inspect $tag >/dev/null 2>&1 && docker tag $tag $previous || true; }"
    }
    Invoke-Remote ($previousTags -join ' && ')

    Write-Host '==> Loading images + restarting the stack…' -ForegroundColor Cyan
    # Only the prune is allowed to fail — it is housekeeping. The `|| true` is braced to it
    # alone: unbraced, it (and the `; echo`) covered the whole && chain, so a failed load or
    # compose up still exited 0 and printed DEPLOYED.
    Invoke-Remote "cd '$RemoteDir' && docker load -i lnshelf-img.tar && rm -f lnshelf-img.tar && docker compose up -d --no-build && { docker image prune -f >/dev/null 2>&1 || true; } && echo DEPLOYED"
}
finally {
    Remove-Item $tar -ErrorAction SilentlyContinue
}

Write-Host '==> Verifying…' -ForegroundColor Cyan
# The shell first — nginx serving index.html. Then the health endpoint through the proxy, the one
# API route open without signing in; it answers 200 only when the API can reach Postgres, so it
# proves nginx, the API and the database are all talking.
$shellOk = Wait-ForHttpOk -Uri $Url
$apiOk   = $shellOk -and (Wait-ForHttpOk -Uri "$Url/api/health" -Attempts 10)

$stopwatch.Stop()
$seconds = $stopwatch.Elapsed.TotalSeconds

if ($shellOk -and $apiOk) {
    Write-Host ("==> Deployed in {0:n0}s. {1} is up and the API and database answer through the proxy. Hard-refresh the browser (cached index.html)." -f $seconds, $Url) -ForegroundColor Green
}
elseif ($shellOk) {
    Write-Warning ("The shell at {0} is up, but {0}/api/health did not return 200. Check the api container: ssh {1} 'cd {2} && docker compose logs api --tail 50'." -f $Url, $target, $RemoteDir)
    # Non-zero, so a scripted deploy does not report success for an API that is down.
    exit 1
}
else {
    Write-Warning ("Images shipped and the stack restarted, but {0} did not return 200 yet. Give it a few seconds (migrations run on startup), then hard-refresh. If it stays down: ssh {1} 'cd {2} && docker compose ps'." -f $Url, $target, $RemoteDir)
    exit 1
}
