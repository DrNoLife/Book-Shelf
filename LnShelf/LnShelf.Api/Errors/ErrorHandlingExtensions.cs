namespace LnShelf.Api.Errors;

public static class ErrorHandlingExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddLnShelfErrorHandling()
        {
            services.AddProblemDetails();
            services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
            return services;
        }
    }
}
