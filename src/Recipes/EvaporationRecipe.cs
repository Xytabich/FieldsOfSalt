using FieldsOfSalt.Utils;
using Newtonsoft.Json;
using System;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace FieldsOfSalt.Recipes
{
	[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
	public class EvaporationRecipe : IByteSerializable
	{
		[JsonProperty(Required = Required.Always)]
		public LiquidIngredient Input;
		[JsonProperty(Required = Required.Always)]
		public JsonItemStack Output;

		[JsonProperty(Required = Required.Always)]
		public CompositeTexture OutputTexture;

		/// <summary>
		/// How long does it take to complete a recipe at 20C.
		/// The final time depends on the ambient temperature.
		/// Formula: Hours=EvaporationTime/((CurrentTemperature+270)/290). Where 270 is an "absolute zero"
		/// </summary>
		[JsonProperty(Required = Required.Always)]
		public double EvaporationTime;

		public bool Enabled { get; private set; } = false;

		public WaterTightContainableProps InputProps;

		private double invTimeMult;
		private double invTemperatureMult;
		private double temperatureMultCap;
		private double baseTemperature;

		public double GetProgress(double hours, float temperature)
		{
			double mult = Math.Max(temperature + baseTemperature, 0) * invTemperatureMult;
			return hours * invTimeMult * Math.Min(mult * mult, temperatureMultCap);
		}

		public void Init()
		{
			Enabled = true;
			InputProps = BlockLiquidContainerBase.GetContainableProps(Input.ResolvedItemstack);
		}

		public bool Resolve(IWorldAccessor resolver, ModConfig config, string sourceForErrorLogging)
		{
			if(OutputTexture?.Base == null)
			{
				resolver.Logger.Log(EnumLogType.Warning, sourceForErrorLogging + " has no content texture");
				return false;
			}
			if(EvaporationTime <= 0)
			{
				resolver.Logger.Log(EnumLogType.Warning, sourceForErrorLogging + " has the wrong evaporation time, it must be greater than zero");
				return false;
			}
			if(Input.Resolve(resolver, sourceForErrorLogging))
			{
				if(Input.Litres > 0)
				{
					var liquid = BlockLiquidContainerBase.GetContainableProps(Input.ResolvedItemstack);
					if(liquid == null)
					{
						resolver.Logger.Log(EnumLogType.Warning, sourceForErrorLogging + " has a litres parameter in the input ingredient, but no suitable liquid was found");
						return false;
					}

					Input.StackSize = (int)(liquid.ItemsPerLitre * Input.Litres);
					Input.ResolvedItemstack.StackSize = Input.StackSize;
				}

				if(Output.Resolve(resolver, sourceForErrorLogging))
				{
					baseTemperature = -config.BaseTemperature;
					invTimeMult = 1 / EvaporationTime;
					temperatureMultCap = config.MaxSpeedMultiplier;
					invTemperatureMult = 1.0 / (20 + baseTemperature);

					Init();

					if(resolver.Api is ICoreClientAPI capi)
					{
						GraphicUtil.BakeTexture(capi, OutputTexture, sourceForErrorLogging, out _);
					}

					return true;
				}
			}
			return false;
		}

		public void ToBytes(BinaryWriter writer)
		{
			Input.ResolvedItemstack.ToBytes(writer);
			Output.ResolvedItemstack.ToBytes(writer);
			writer.Write(OutputTexture.Base.ToShortString());
			writer.Write(EvaporationTime);
		}

		public void FromBytes(BinaryReader reader, IWorldAccessor resolver)
		{
			if(reader.TryReadLiquidItemStack(resolver, out Input))
			{
				if(reader.TryReadJsonItemStack(resolver, out Output))
				{
					OutputTexture = new CompositeTexture(new AssetLocation(reader.ReadString()));
					EvaporationTime = reader.ReadDouble();

					Init();

					if(resolver.Api is ICoreClientAPI capi)
					{
						GraphicUtil.BakeTexture(capi, OutputTexture, "Evaporation recipe", out _);
					}
				}
			}
		}
	}
}