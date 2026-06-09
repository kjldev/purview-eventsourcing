//using Purview.EventSourcing.Samples.Domain;
//using Purview.EventSourcing.Validation;

//namespace Purview.EventSourcing.Samples.Logic;

//sealed class LocationAggregateRules : AggregateRules<LocationAggregate>
//{
//    public LocationAggregateRules()
//    {
//        Validate(location => location.LocationId.NotNullOrEmpty(), "LocationId is required.");
//        Validate(
//            location => location.LocationName.NotNullOrEmpty(),
//            "LocationName is required."
//        );
//        Transform(location => location.LocationName.Trim().NullOnEmpty());
//    }
//}
