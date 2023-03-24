using FieldsOfSalt.Blocks.Entities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.ServerMods.NoObf;

namespace FieldsOfSalt.Items
{
	public class ItemPondTemplate : Item//TODO: OnItemRender draw horizontal area
	{
		private WorldInteraction[] interactHelp = new WorldInteraction[] { new WorldInteraction() {
			MouseButton = EnumMouseButton.Right,
			ActionLangCode = "fieldsofsalt:template-selectcorners"
		}};

		private ItemStack mainBlock = null;
		private ItemStack surrogateBlock = null;

		private TemplateInfo template = null;
		private bool templateInvalid = false;

		public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
		{
			if(blockSel == null || !firstEvent) return;
			if(api.Side == EnumAppSide.Server && mainBlock != null && surrogateBlock != null)
			{
				var attr = slot.Itemstack.Attributes;
				int? x = attr.TryGetInt("startX");
				int? y = attr.TryGetInt("startY");
				int? z = attr.TryGetInt("startZ");
				if(x.HasValue && y.HasValue && z.HasValue)
				{
					attr.RemoveAttribute("startX");
					attr.RemoveAttribute("startY");
					attr.RemoveAttribute("startZ");

					var fromPos = new BlockPos(
						Math.Min(x.Value, blockSel.Position.X),
						y.Value,
						Math.Min(z.Value, blockSel.Position.Z)
					);
					var toPos = new BlockPos(
						Math.Max(x.Value, blockSel.Position.X),
						y.Value,
						Math.Max(z.Value, blockSel.Position.Z)
					);
					TryCreateStructure(fromPos, toPos);
				}
				else
				{
					attr.SetInt("startX", blockSel.Position.X);
					attr.SetInt("startY", blockSel.Position.Y);
					attr.SetInt("startZ", blockSel.Position.Z);
					(api as ICoreClientAPI)?.ShowChatMessage("First point selected at: ");
					slot.MarkDirty();
				}
			}
			handling = EnumHandHandling.Handled;
		}

		public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
		{
			return false;
		}

		public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
		{
			return interactHelp;
		}

		private void TryCreateStructure(BlockPos fromPos, BlockPos toPos)
		{
			EnsureTemplate();
			if(templateInvalid) return;

			//TODO: check structure
			BlockEntityPond.CreateStructure(api.World, fromPos, toPos, mainBlock, surrogateBlock);
		}

		private void EnsureTemplate()
		{
			if(templateInvalid) return;
			templateInvalid = true;

			if(Attributes == null) return;

			try
			{
				template = Attributes["template"].AsObject<TemplateInfo>(null, Code.Domain);
				if(template == null)
				{
					throw new Exception("Invalid template format");
				}
				if(!template.Resolve(api.World))
				{
					template = null;
					throw new Exception("Unable to resolve template");
				}

				var blocks = Attributes["multiblock"].AsObject<MultiblockInfo>(null, Code.Domain);
				if(blocks == null)
				{
					template = null;
					throw new Exception("Invalid multiblock format");
				}
				if(!blocks.Resolve(api.World))
				{
					template = null;
					throw new Exception("Unable to resolve multiblock parts");
				}

				mainBlock = blocks.Main.ResolvedItemstack;
				surrogateBlock = blocks.Surrogate.ResolvedItemstack;

				templateInvalid = false;
			}
			catch(Exception e)
			{
				api.Logger.Log(EnumLogType.Error, "Exception when trying to parse a multiblock template for {0}\n{1}", Code, e);
			}
		}

		private class TemplateInfo
		{
			[JsonProperty(Required = Required.Always)]
			public BlockTemplate Border;
			[JsonProperty(Required = Required.Always)]
			public BlockTemplate Bottom;
			[JsonProperty(Required = Required.Always)]
			public BlockTemplate Connector;
			[JsonProperty(Required = Required.Always)]
			public int MaxSize;

			public class BlockTemplate : RegistryObjectType
			{
				public HashSet<Block> BlockVariants;

				public bool Resolve(IWorldAccessor world, StringBuilder tmpSb, Stack<StateRecord> stateStack, HashSet<string> tmpStates,
					List<string> variantStates, List<VariantGroup> variantGroups, ref char[] tmpChars, ref Dictionary<AssetLocation, StandardWorldProperty> worldProperties)
				{
					if(VariantGroups != null && VariantGroups.Length > 0)
					{
						variantStates.Clear();
						variantGroups.Clear();
						for(int i = 0; i < VariantGroups.Length; i++)
						{
							var vg = VariantGroups[i];
							if(string.IsNullOrEmpty(vg.Code))
							{
								world.Api.Logger.Log(EnumLogType.Error, "Template block {0} defined a variantgroup, but did not explicitly declare a code for this variant group. Ignoring.", Code);
								continue;
							}
							tmpStates.Clear();
							LoadProperties(world.Api, vg.LoadFromProperties, tmpStates, ref worldProperties);
							LoadProperties(world.Api, vg.LoadFromPropertiesCombine, tmpStates, ref worldProperties);
							if(vg.States != null)
							{
								foreach(var state in vg.States)
								{
									tmpStates.Add(state);
								}
							}
							if(tmpStates.Count > 0)
							{
								variantGroups.Add(new VariantGroup(vg.Code, variantStates.Count, tmpStates.Count));
								variantStates.AddRange(tmpStates);
							}
						}
					}
					if(variantGroups.Count > 0)
					{
						tmpStates.Clear();
						tmpSb.Append(Code.ToString());
						var group = variantGroups[0];
						for(int i = 0; i < group.Size; i++)
						{
							stateStack.Push(new StateRecord(group.Index + i, 0, 0, tmpSb.Length));
						}
						while(stateStack.Count > 0)
						{
							var record = stateStack.Peek();
							int index = record.SbIndex + record.SbLength;
							if(index >= tmpSb.Length)
							{
								tmpSb.Remove(index, tmpSb.Length - index);
							}
							group = variantGroups[record.GroupIndex];
							ClonePartAndReplace(tmpSb, record.SbIndex, record.SbLength, group.Name, variantStates[record.StateIndex], ref tmpChars);
							if(record.GroupIndex + 1 >= variantGroups.Count)
							{
								tmpStates.Add(tmpSb.ToString(index, tmpSb.Length - index));
								stateStack.Pop();
								if(record.StateIndex + 1 >= group.Index + group.Size)
								{
									while(stateStack.Count > 0)
									{
										record = stateStack.Peek();
										group = variantGroups[record.GroupIndex];
										if(record.StateIndex + 1 >= group.Index + group.Size)
										{
											stateStack.Pop();
										}
										else break;
									}
								}
							}
							else
							{
								group = variantGroups[record.GroupIndex + 1];
								for(int i = 0; i < group.Size; i++)
								{
									index = record.SbIndex + record.SbLength;
									stateStack.Push(new StateRecord(group.Index + i, record.GroupIndex + 1, index, tmpSb.Length - index));
								}
							}
						}
					}
					if(BlockVariants == null)
					{
						var block = world.GetBlock(Code);
						if(block != null)
						{
							BlockVariants = new HashSet<Block>();
							BlockVariants.Add(block);
						}
					}
					return BlockVariants != null;
				}

				private static void LoadProperties(ICoreAPI api, AssetLocation[] propsLocations, HashSet<string> variantStates,
					ref Dictionary<AssetLocation, StandardWorldProperty> worldProperties)
				{
					if(propsLocations == null) return;
					for(int i = 0; i < propsLocations.Length; i++)
					{
						LoadProperties(api, propsLocations[i], variantStates, ref worldProperties);
					}
					LoadWorldPropertiesIfNeeded(api, ref worldProperties);
				}

				private static void LoadProperties(ICoreAPI api, AssetLocation propsLocation, HashSet<string> variantStates,
					ref Dictionary<AssetLocation, StandardWorldProperty> worldProperties)
				{
					if(propsLocation == null) return;
					LoadWorldPropertiesIfNeeded(api, ref worldProperties);
					if(worldProperties.TryGetValue(propsLocation, out var properties) && properties != null)
					{
						for(int i = 0; i < properties.Variants.Length; i++)
						{
							variantStates.Add(properties.Variants[i].Code.Path);
						}
					}
				}

				private static void LoadWorldPropertiesIfNeeded(ICoreAPI api, ref Dictionary<AssetLocation, StandardWorldProperty> worldProperties)
				{
					if(worldProperties == null)
					{
						worldProperties = new Dictionary<AssetLocation, StandardWorldProperty>();
						foreach(var pair in api.Assets.GetMany<StandardWorldProperty>(api.Logger, "worldproperties/"))
						{
							AssetLocation loc = pair.Key.Clone();
							loc.Path = loc.Path.Replace("worldproperties/", "");
							loc.RemoveEnding();
							pair.Value.Code.Domain = pair.Key.Domain;
							worldProperties.Add(loc, pair.Value);
						}
					}
				}

				private static void ClonePartAndReplace(StringBuilder sb, int index, int length, string pattern, string value, ref char[] tmpChars)
				{
					if(tmpChars == null || tmpChars.Length < length)
					{
						tmpChars = new char[length];
					}
					sb.CopyTo(index, tmpChars, 0, length);
					int prevIndex = 0;
					int startIndex = Array.IndexOf(tmpChars, '{', 0, length);
					while(startIndex >= 0)
					{
						int endIndex = Array.IndexOf(tmpChars, '}', startIndex, length - startIndex);
						if(endIndex >= 0)
						{
							int strLen = endIndex - startIndex - 1;
							if(strLen == pattern.Length && pattern.Equals(new string(tmpChars, startIndex + 1, strLen), StringComparison.InvariantCultureIgnoreCase))// .net 7 when...
							{
								if(startIndex > prevIndex) sb.Append(tmpChars, prevIndex, startIndex - prevIndex);
								sb.Append(value);
								prevIndex = endIndex + 1;
							}
							startIndex = Array.IndexOf(tmpChars, '{', endIndex, length - endIndex);
						}
						else break;
					}
					sb.Append(tmpChars, 0, length);
				}
			}

			public bool Resolve(IWorldAccessor world)
			{
				var sb = new StringBuilder(1024);
				var stateStack = new Stack<StateRecord>();
				var tmpStates = new HashSet<string>();
				var variantStates = new List<string>();
				var variantGroups = new List<VariantGroup>();
				char[] tmpChars = null;
				Dictionary<AssetLocation, StandardWorldProperty> worldProperties = null;
				if(!Border.Resolve(world, sb, stateStack, tmpStates, variantStates, variantGroups, ref tmpChars, ref worldProperties)) return false;
				if(!Bottom.Resolve(world, sb, stateStack, tmpStates, variantStates, variantGroups, ref tmpChars, ref worldProperties)) return false;
				return Connector.Resolve(world, sb, stateStack, tmpStates, variantStates, variantGroups, ref tmpChars, ref worldProperties);
			}
		}

		private class MultiblockInfo
		{
			public JsonItemStack Main;
			public JsonItemStack Surrogate;

			public bool Resolve(IWorldAccessor world)
			{
				if(!Main.Resolve(world, "pond multiblock main")) return false;
				if(!Surrogate.Resolve(world, "pond multiblock surrogate")) return false;
				return Main.ResolvedItemstack != null && Surrogate.ResolvedItemstack != null;
			}
		}

		private readonly struct VariantGroup
		{
			public readonly string Name;
			public readonly int Index;
			public readonly int Size;

			public VariantGroup(string name, int index, int size)
			{
				Name = name;
				Index = index;
				Size = size;
			}
		}

		private readonly struct StateRecord
		{
			public readonly int StateIndex;
			public readonly int GroupIndex;
			public readonly int SbIndex;
			public readonly int SbLength;

			public StateRecord(int stateIndex, int groupIndex, int sbIndex, int sbLength)
			{
				StateIndex = stateIndex;
				GroupIndex = groupIndex;
				SbIndex = sbIndex;
				SbLength = sbLength;
			}
		}
	}
}