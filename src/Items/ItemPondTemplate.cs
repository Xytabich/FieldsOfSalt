using FieldsOfSalt.Blocks;
using FieldsOfSalt.Blocks.Entities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;

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
			if(api.Side == EnumAppSide.Server)
			{
				EnsureTemplate();
				if(templateInvalid) return;

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
					TryCreateStructure(fromPos, toPos, (byEntity as EntityPlayer)?.Player as IServerPlayer);
				}
				else
				{
					attr.SetInt("startX", blockSel.Position.X);
					attr.SetInt("startY", blockSel.Position.Y);
					attr.SetInt("startZ", blockSel.Position.Z);
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

		private void TryCreateStructure(BlockPos fromPos, BlockPos toPos, IServerPlayer player)
		{
			if(toPos.X - fromPos.X < 4 || toPos.Z - fromPos.Z < 4)// to(4)-from(0) = 4
			{
				if(player != null) (api as ICoreServerAPI).SendIngameError(player, "fieldsofsalt:structuretoosmall");
				return;
			}
			if((toPos.X - fromPos.X + 1) > template.MaxSize || (toPos.Z - fromPos.Z + 1) > template.MaxSize)
			{
				if(player != null) (api as ICoreServerAPI).SendIngameError(player, "fieldsofsalt:structuretoobig");
				return;
			}
			if(((toPos.X - fromPos.X) & 1) != 0 || ((toPos.Z - fromPos.Z) & 1) != 0)
			{
				if(player != null) (api as ICoreServerAPI).SendIngameError(player, "fieldsofsalt:structureoddsides");
				return;
			}

			var accessor = api.World.GetLockFreeBlockAccessor();
			bool isInvalid = BlockEntityPond.IterateStructureBlocks(fromPos, toPos,
				(tmpPos, face) => CheckBorderInvalid(accessor, tmpPos, face, player),
				tmpPos => {
					var block = accessor.GetBlock(tmpPos);
					if(template.Bottom.BlockVariants.Contains(block.Id))
					{
						return false;
					}
					if(player != null) (api as ICoreServerAPI).SendIngameError(player, "fieldsofsalt:invalidbottomblock");
					return true;
				}
			);
			if(isInvalid) return;

			BlockEntityPond.CreateStructure(api.World, fromPos, toPos, mainBlock, surrogateBlock);
		}

		private bool CheckBorderInvalid(IBlockAccessor accessor, BlockPos pos, BlockFacing facing, IServerPlayer player)
		{
			var block = accessor.GetBlock(pos);
			if(!template.Border.BlockVariants.Contains(block.Id))
			{
				if(template.Connector.BlockVariants.Contains(block.Id))
				{
					return false;
				}
				if((block is ILiquidSinkConnector connector) && connector.CanConnect(accessor, pos, facing))
				{
					return false;
				}
				if(player != null) (api as ICoreServerAPI).SendIngameError(player, "fieldsofsalt:invalidborderblock");
				return true;
			}
			return false;
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

			[JsonObject(MemberSerialization.OptIn)]
			public class BlockTemplate
			{
				[JsonProperty(Required = Required.Always)]
				public AssetLocation Code;

				[JsonProperty]
				public RegistryObjectVariantGroup[] VariantGroups;

				[JsonProperty]
				public AssetLocation[] SkipVariants;

				[JsonProperty]
				public AssetLocation[] AllowedVariants;

				public HashSet<int> BlockVariants;

				public bool Resolve(IWorldAccessor world, StringBuilder tmpSb, Stack<StateRecord> stateStack, HashSet<string> tmpStates, HashSet<AssetLocation> tmpCodes1, HashSet<AssetLocation> tmpCodes2, List<string> variantStates, List<VariantGroup> variantGroups, ref char[] tmpChars, ref Dictionary<AssetLocation, StandardWorldProperty> worldProperties)
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
						tmpSb.Clear();

						tmpSb.Append(Code.ToString());
						var group = variantGroups[0];
						for(int i = 0; i < group.Size; i++)
						{
							stateStack.Push(new StateRecord(group.Index + i, 0, 0, tmpSb.Length));
						}
						var codes = tmpCodes1;
						codes.Clear();
						while(stateStack.Count > 0)
						{
							var record = stateStack.Peek();
							int index = record.SbIndex + record.SbLength;
							if(index < tmpSb.Length)
							{
								tmpSb.Remove(index, tmpSb.Length - index);
							}
							group = variantGroups[record.GroupIndex];
							ClonePartAndReplace(tmpSb, record.SbIndex, record.SbLength, group.Name, variantStates[record.StateIndex], ref tmpChars);
							if(record.GroupIndex + 1 >= variantGroups.Count)
							{
								codes.Add(new AssetLocation(tmpSb.ToString(index, tmpSb.Length - index)));
								stateStack.Pop();
								if(record.StateIndex == group.Index)
								{
									while(stateStack.Count > 0)
									{
										record = stateStack.Pop();
										group = variantGroups[record.GroupIndex];
										if(record.StateIndex + 1 < group.Index + group.Size)
										{
											break;
										}
									}
								}
							}
							else
							{
								int groupIndex = record.GroupIndex + 1;
								group = variantGroups[groupIndex];
								for(int i = 0; i < group.Size; i++)
								{
									index = record.SbIndex + record.SbLength;
									stateStack.Push(new StateRecord(group.Index + i, groupIndex, index, tmpSb.Length - index));
								}
							}
						}
						if(tmpCodes1.Count > 0)
						{
							var tmpCodes = tmpCodes2;
							if(AllowedVariants != null)
							{
								tmpCodes.Clear();
								foreach(var code in codes)
								{
									foreach(var variant in AllowedVariants)
									{
										if(WildcardUtil.Match(variant, code))
										{
											tmpCodes.Add(code);
											break;
										}
									}
								}
								var tmp = tmpCodes;
								tmpCodes = codes;
								codes = tmp;
							}
							if(SkipVariants != null)
							{
								tmpCodes.Clear();
								foreach(var code in codes)
								{
									bool skip = false;
									foreach(var variant in SkipVariants)
									{
										if(WildcardUtil.Match(variant, code))
										{
											skip = true;
											break;
										}
									}
									if(skip) continue;
									tmpCodes.Add(code);
								}
								codes = tmpCodes;
							}
							foreach(var code in codes)
							{
								var block = world.GetBlock(code);
								if(block != null)
								{
									if(BlockVariants == null) BlockVariants = new HashSet<int>();
									BlockVariants.Add(block.Id);
								}
							}
						}
					}
					if(BlockVariants == null)
					{
						var block = world.GetBlock(Code);
						if(block != null)
						{
							BlockVariants = new HashSet<int>();
							BlockVariants.Add(block.Id);
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
					if(prevIndex < length) sb.Append(tmpChars, prevIndex, length - prevIndex);
				}
			}

			public bool Resolve(IWorldAccessor world)
			{
				var sb = new StringBuilder(1024);
				var stateStack = new Stack<StateRecord>();
				var tmpStates = new HashSet<string>();
				var tmpCodes1 = new HashSet<AssetLocation>();
				var tmpCodes2 = new HashSet<AssetLocation>();
				var variantStates = new List<string>();
				var variantGroups = new List<VariantGroup>();
				char[] tmpChars = null;
				Dictionary<AssetLocation, StandardWorldProperty> worldProperties = null;
				if(!Border.Resolve(world, sb, stateStack, tmpStates, tmpCodes1, tmpCodes2, variantStates, variantGroups, ref tmpChars, ref worldProperties)) return false;
				if(!Bottom.Resolve(world, sb, stateStack, tmpStates, tmpCodes1, tmpCodes2, variantStates, variantGroups, ref tmpChars, ref worldProperties)) return false;
				return Connector.Resolve(world, sb, stateStack, tmpStates, tmpCodes1, tmpCodes2, variantStates, variantGroups, ref tmpChars, ref worldProperties);
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