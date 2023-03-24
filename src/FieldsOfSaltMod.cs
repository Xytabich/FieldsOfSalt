using FieldsOfSalt.Blocks;
using FieldsOfSalt.Blocks.Entities;
using FieldsOfSalt.Items;
using System.Collections.Concurrent;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Common.Database;

namespace FieldsOfSalt
{
	public class FieldsOfSaltMod : ModSystem
	{
		private ConcurrentDictionary<Xyz, Xyz> pos2main = new ConcurrentDictionary<Xyz, Xyz>();

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
		}

		public bool GetReferenceToMainBlock(BlockPos fromPos, BlockPos outMainPos = null)
		{
			if(pos2main.TryGetValue(new Xyz(fromPos.X, fromPos.Y, fromPos.Z), out var mainPos))
			{
				if(outMainPos != null)
				{
					outMainPos.X = mainPos.X;
					outMainPos.Y = mainPos.Y;
					outMainPos.Z = mainPos.Z;
				}
				return true;
			}
			return false;
		}

		public bool AddReferenceToMainBlock(BlockPos fromPos, BlockPos mainPos)
		{
			return pos2main.TryAdd(new Xyz(fromPos.X, fromPos.Y, fromPos.Z), new Xyz(mainPos.X, mainPos.Y, mainPos.Z));
		}

		public bool RemoveReferenceToMainBlock(BlockPos fromPos, BlockPos mainPos)
		{
			if(pos2main.TryGetValue(new Xyz(fromPos.X, fromPos.Y, fromPos.Z), out var mPos))
			{
				if(mPos.X == mainPos.X && mPos.Y == mainPos.Y && mPos.Z == mainPos.Z)
				{
					pos2main.TryRemove(new Xyz(fromPos.X, fromPos.Y, fromPos.Z), out mPos);
					return true;
				}
			}
			return false;
		}
	}
}