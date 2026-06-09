//using Microsoft.Extensions.DependencyInjection;

//namespace Purview.EventSourcing.Validation;

//public static class Validate
//{
//    public static IValidationBuilder<T> For<T>() => new ValidationBuilder<T>();

//    public static IValidationBuilder<T> For<T>(IServiceCollection services)
//    {
//        ArgumentNullException.ThrowIfNull(services);

//        var builder = new ValidationBuilder<T>();
//        services.AddSingleton<IValidationBuilder<T>>(builder);
//        services.AddSingleton(builder);
//        return builder;
//    }
//}
