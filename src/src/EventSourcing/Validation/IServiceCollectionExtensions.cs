//using System.ComponentModel;
//using Microsoft.Extensions.DependencyInjection;

//namespace Purview.EventSourcing.Validation;

//[EditorBrowsable(EditorBrowsableState.Never)]
//public static class IServiceCollectionExtensions
//{
//    public static IServiceCollection AddAggregateValidation(this IServiceCollection services)
//    {
//        ArgumentNullException.ThrowIfNull(services);
//        return services;
//    }

//    public static IServiceCollection AddAggregateValidation<T>(
//        this IServiceCollection services,
//        Action<IValidationBuilder<T>>? configure = null
//    )
//    {
//        ArgumentNullException.ThrowIfNull(services);

//        var builder = Validate.For<T>(services);
//        configure?.Invoke(builder);
//        return services;
//    }
//}
