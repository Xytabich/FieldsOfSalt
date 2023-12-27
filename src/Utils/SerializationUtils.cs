using FieldsOfSalt.Recipes;
using System.IO;
using Vintagestory.API.Common;

namespace FieldsOfSalt.Utils
{
	public static class SerializationUtils
	{
		public static bool TryReadJsonItemStack(this BinaryReader reader, IWorldAccessor resolver, out JsonItemStack jsonStack)
		{
			var stack = new ItemStack(reader);
			if(stack.ResolveBlockOrItem(resolver))
			{
				jsonStack = new JsonItemStack() {
					Type = stack.Class,
					Code = stack.Collectible.Code,
					StackSize = stack.StackSize,
					ResolvedItemstack = stack
				};
				return true;
			}

			jsonStack = null;
			return false;
		}

		public static bool TryReadLiquidItemStack(this BinaryReader reader, IWorldAccessor resolver, out LiquidIngredient jsonStack)
		{
			var stack = new ItemStack(reader);
			if(stack.ResolveBlockOrItem(resolver))
			{
				jsonStack = new LiquidIngredient() {
					Type = stack.Class,
					Code = stack.Collectible.Code,
					StackSize = stack.StackSize,
					ResolvedItemstack = stack
				};
				return true;
			}

			jsonStack = null;
			return false;
		}
	}
}