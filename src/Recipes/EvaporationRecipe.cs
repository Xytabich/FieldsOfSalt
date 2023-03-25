﻿using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace FieldsOfSalt.Recipes
{
	[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
	public class EvaporationRecipe
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

		public WaterTightContainableProps InputProps;

		private double CalTempMult;

		public double GetProgress(double hours, float temperature)
		{
			return hours * (temperature + 270) * CalTempMult;
		}

		public void Init()
		{
			CalTempMult = 1.0 / (EvaporationTime * 290);
			InputProps = BlockLiquidContainerBase.GetContainableProps(Input.ResolvedItemstack);
		}
	}
}