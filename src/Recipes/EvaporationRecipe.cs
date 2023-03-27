using FieldsOfSalt.Utils;
using Newtonsoft.Json;
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
		public JsonItemStack Input;
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

		private double CalTempMult;

		public double GetProgress(double hours, float temperature)
		{
			return hours * (temperature + 270) * CalTempMult;
		}

		public void Init()
		{
			Enabled = true;
			CalTempMult = 1.0 / (EvaporationTime * 290);
			InputProps = BlockLiquidContainerBase.GetContainableProps(Input.ResolvedItemstack);
		}

		public bool Resolve(IWorldAccessor resolver, string sourceForErrorLogging)
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
				if(Output.Resolve(resolver, sourceForErrorLogging))
				{
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
			if(reader.TryReadJsonItemStack(resolver, out Input))
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