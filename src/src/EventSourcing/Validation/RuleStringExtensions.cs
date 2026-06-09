//using System.ComponentModel.DataAnnotations;

//namespace Purview.EventSourcing.Validation;

///// <summary>
///// String helpers intended for AggregateRules transformation/validation expressions.
///// </summary>
//public static class RuleStringExtensions
//{
//    public readonly record struct RuleString(string? Value, bool RequireEmailAddress = false);

//    public static bool NotNullOrEmpty(this string? value) => !string.IsNullOrWhiteSpace(value);

//    public static bool NotNullOrEmpty(this RuleString value) =>
//        !string.IsNullOrWhiteSpace(value.Value)
//        && (!value.RequireEmailAddress || new EmailAddressAttribute().IsValid(value.Value));

//    public static string? NullOnEmpty(this string? value) =>
//        string.IsNullOrWhiteSpace(value) ? null : value;

//    public static string? Lowered(this string? value) => value?.ToLowerInvariant();

//    /// <summary>
//    /// Identity method used to explicitly signal email-format checks in validation chains.
//    /// </summary>
//    public static RuleString EmailAddress(this string? value) =>
//        new(value, RequireEmailAddress: true);

//    public static bool IsValidEmailAddress(this string? value) =>
//        !string.IsNullOrWhiteSpace(value) && new EmailAddressAttribute().IsValid(value);
//}
