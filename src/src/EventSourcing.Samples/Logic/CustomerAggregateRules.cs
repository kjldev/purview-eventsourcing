//using Purview.EventSourcing.Samples.Domain;
//using Purview.EventSourcing.Validation;

//namespace Purview.EventSourcing.Samples.Logic;

//sealed class CustomerAggregateRules : AggregateRules<CustomerAggregate>
//{
//    public CustomerAggregateRules()
//    {
//        Validate(customer => customer.Email.EmailAddress().NotNullOrEmpty(), "Email is invalid.");
//        Transform(customer => customer.Email.Trim().Lowered().NullOnEmpty());
//    }
//}
