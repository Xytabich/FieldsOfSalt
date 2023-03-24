using Newtonsoft.Json;
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

		/// <summary>
		/// How long does it take to complete a recipe at 20C.
		/// The final time depends on the ambient temperature.
		/// Formula: Hours=EvaporationTime*(CurrentTemperature+270)/290. Where 270 is an "absolute zero"
		/// </summary>
		[JsonProperty(Required = Required.Always)]
		public double EvaporationTime;

		public WaterTightContainableProps InputProps;

		public double GetProgress(double hours, float temperature)
		{
			return hours * (290.0 / (temperature + 270.0));
		}
	}
}