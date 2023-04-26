using Cairo;
using FieldsOfSalt.Recipes;
using System;
using System.Collections.Generic;
using System.Globalization;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace FieldsOfSalt.Handbook
{
	public class EvaporationInfo : IDisposable
	{
		private FieldsOfSaltMod mod;
		private List<EvaporationRecipe> tmpList = new List<EvaporationRecipe>();

		public EvaporationInfo(FieldsOfSaltMod mod)
		{
			this.mod = mod;
			HandbookItemInfoEvent.onGetHandbookInfo += GetHandbookInfo;
		}

		public void Dispose()
		{
			HandbookItemInfoEvent.onGetHandbookInfo -= GetHandbookInfo;
		}

		private void GetHandbookInfo(ItemSlot inSlot, ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor, HandbookItemInfoSection section, List<RichTextComponentBase> outComponents)
		{
			if(section != HandbookItemInfoSection.BeforeExtraSections) return;
			tmpList.Clear();
			mod.GetRecipesByOutput(inSlot.Itemstack, tmpList);
			if(tmpList.Count > 0)
			{
				outComponents.Add(new ClearFloatTextComponent(capi, 7f));
				outComponents.Add(new RichTextComponent(capi, Lang.Get("fieldsofsalt:Obtained by evaporation") + "\n", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold)));
				foreach(var recipe in tmpList)
				{
					var element = new SlideshowItemstackTextComponent(capi, new ItemStack[] { recipe.Input.ResolvedItemstack }, 40.0,
						EnumFloat.Inline, cs => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)));
					element.ShowStackSize = recipe.Input.ResolvedItemstack.StackSize > 1;
					element.PaddingRight = GuiElement.scaled(10.0);

					outComponents.Add(new RichTextComponent(capi, Lang.Get("fieldsofsalt:{0}x evaporating", recipe.Output.ResolvedItemstack.StackSize) + " ", CairoFont.WhiteSmallText()));
					outComponents.Add(element);
					outComponents.Add(new RichTextComponent(capi, " " + Lang.Get("fieldsofsalt:within {0} hours",
						recipe.EvaporationTime.ToString("G", CultureInfo.InvariantCulture)) + "\n", CairoFont.WhiteSmallText()));
				}
				outComponents.Add(new ClearFloatTextComponent(capi, 7f));
				tmpList.Clear();
			}
		}
	}
}