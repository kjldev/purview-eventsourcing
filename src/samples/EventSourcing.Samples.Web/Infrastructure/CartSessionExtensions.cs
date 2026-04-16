using System.Text.Json;
using Purview.EventSourcing.Samples.Services;

namespace Purview.EventSourcing.Samples.Web.Infrastructure;

public static class CartSessionExtensions
{
	const string CartKey = "cart";

	public static List<CartItem> GetCart(this ISession session)
	{
		var json = session.GetString(CartKey);
		return string.IsNullOrEmpty(json) ? [] : JsonSerializer.Deserialize<List<CartItem>>(json) ?? [];
	}

	public static void SetCart(this ISession session, List<CartItem> cart) =>
		session.SetString(CartKey, JsonSerializer.Serialize(cart));

	public static int GetCartCount(this ISession session) => session.GetCart().Sum(c => c.Quantity);
}
