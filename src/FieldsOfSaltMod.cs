using FieldsOfSalt.Blocks;
using FieldsOfSalt.Blocks.Entities;
using FieldsOfSalt.Handbook;
using FieldsOfSalt.Items;
using FieldsOfSalt.Recipes;
using FieldsOfSalt.Renderer;
using FieldsOfSalt.Utils;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace FieldsOfSalt
{
	public class FieldsOfSaltMod : ModSystem
	{
		public TemplateAreaRenderer TemplateAreaRenderer => templateAreaRenderer;

		private RecipeRegistryGeneric<EvaporationRecipe> pondRecipes;

		private TemplateAreaRenderer templateAreaRenderer = null;

		private Harmony harmony = null;
		private List<IDisposable> handbookInfoList = null;

		public override double ExecuteOrder()
		{
			return 1.0;
		}

		public override void Start(ICoreAPI api)
		{
			base.Start(api);
			api.RegisterBlockClass("fieldsofsalt:channel", typeof(BlockChannel));
			api.RegisterBlockClass("fieldsofsalt:connector", typeof(BlockConnector));
			api.RegisterBlockClass("fieldsofsalt:source", typeof(BlockSource));
			api.RegisterBlockClass("fieldsofsalt:rendersurrogate", typeof(BlockRenderSurrogate));
			api.RegisterBlockClass("fieldsofsalt:pond", typeof(BlockPond));

			api.RegisterItemClass("fieldsofsalt:pondtemplate", typeof(ItemPondTemplate));

			api.RegisterBlockEntityClass("fieldsofsalt:source", typeof(BlockEntitySource));
			api.RegisterBlockEntityClass("fieldsofsalt:pond", typeof(BlockEntityPond));

			pondRecipes = api.RegisterRecipeRegistry<RecipeRegistryGeneric<EvaporationRecipe>>("evaporationpondrecipes");
		}

		public override void StartClientSide(ICoreClientAPI api)
		{
			base.StartClientSide(api);

			try
			{
				harmony = new Harmony("fieldsofsalt");
				harmony.PatchAll(typeof(FieldsOfSaltMod).Assembly);
			}
			catch(Exception e)
			{
				api.Logger.Error(e.Message);
			}

			handbookInfoList = new List<IDisposable>();
			handbookInfoList.Add(new EvaporationInfo(this));

			templateAreaRenderer = new TemplateAreaRenderer(api);
		}

		public override void AssetsLoaded(ICoreAPI api)
		{
			base.AssetsLoaded(api);
			if(api is ICoreServerAPI sapi)
			{
				var config = sapi.LoadModConfig<ModConfig>("fieldsofsalt.json") ?? new ModConfig();
				var many = api.Assets.GetMany<JToken>(sapi.Server.Logger, "recipes/evaporationpond");
				int quantityRegistered = 0;
				int quantityIgnored = 0;
				foreach(var pair in many)
				{
					if(pair.Value is JObject)
					{
						if(LoadPondRecipe(sapi, config, pair.Key, pair.Value))
						{
							quantityRegistered++;
						}
						else
						{
							quantityIgnored++;
						}
					}
					if(pair.Value is JArray arr)
					{
						foreach(var token in arr)
						{
							if(LoadPondRecipe(sapi, config, pair.Key, pair.Value))
							{
								quantityRegistered++;
							}
							else
							{
								quantityIgnored++;
							}
						}
					}
				}
				api.World.Logger.Event("{0} evaporation pond recipes loaded{1}", quantityRegistered, (quantityIgnored != 0) ? $" ({quantityIgnored} could not be resolved)" : "");
			}
		}

		public override void Dispose()
		{
			base.Dispose();
			templateAreaRenderer?.Dispose();

			if(handbookInfoList != null)
			{
				foreach(var info in handbookInfoList)
				{
					info.Dispose();
				}
			}
			harmony?.UnpatchAll("fieldsofsalt");
		}

		public void GetRecipesByOutput(ItemStack itemstack, ICollection<EvaporationRecipe> outItems)
		{
			foreach(var recipe in pondRecipes.Recipes)
			{
				if(recipe.Enabled && recipe.Output.ResolvedItemstack.Satisfies(itemstack))
				{
					outItems.Add(recipe);
				}
			}
		}

		public bool TryGetRecipe(ItemStack forLiquid, out EvaporationRecipe evaporationRecipe)
		{
			foreach(var recipe in pondRecipes.Recipes)
			{
				if(recipe.Enabled && recipe.Input.ResolvedItemstack.Satisfies(forLiquid))
				{
					evaporationRecipe = recipe;
					return true;
				}
			}

			evaporationRecipe = null;
			return false;
		}

		private bool LoadPondRecipe(ICoreServerAPI api, ModConfig config, AssetLocation location, JToken json)
		{
			var recipe = json.ToObject<EvaporationRecipe>(location.Domain);
			if(recipe.Resolve(api.World, config, "pond evaporation recipe " + location))
			{
				pondRecipes.Recipes.Add(recipe);
				return true;
			}
			return false;
		}
	}
}