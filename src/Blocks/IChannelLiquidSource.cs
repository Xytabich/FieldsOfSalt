namespace FieldsOfSalt.Blocks
{
	public interface IChannelLiquidSource : ILiquidChannel
	{
		float LitresPerTickGeneration { get; }
	}
}