using System.Collections.Concurrent;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Common.Database;

namespace FieldsOfSalt.Multiblock
{
	public class MultiblockManager : ModSystem
	{
		private readonly ConcurrentDictionary<Xyz, Xyz> pos2main = new ConcurrentDictionary<Xyz, Xyz>();

		public bool GetReferenceToMainBlock(BlockPos fromPos, BlockPos outMainPos = null)
		{
			if(pos2main.TryGetValue(new Xyz(fromPos.X, fromPos.InternalY, fromPos.Z), out var mainPos))
			{
				if(outMainPos != null)
				{
					outMainPos.X = mainPos.X;
					outMainPos.InternalY = mainPos.Y;
					outMainPos.Z = mainPos.Z;
				}
				return true;
			}
			return false;
		}

		public bool AddReferenceToMainBlock(BlockPos fromPos, BlockPos mainPos)
		{
			return pos2main.TryAdd(new Xyz(fromPos.X, fromPos.InternalY, fromPos.Z), new Xyz(mainPos.X, mainPos.InternalY, mainPos.Z));
		}

		public bool RemoveReferenceToMainBlock(BlockPos fromPos, BlockPos mainPos)
		{
			if(pos2main.TryGetValue(new Xyz(fromPos.X, fromPos.InternalY, fromPos.Z), out var mPos))
			{
				if(mPos.X == mainPos.X && mPos.Y == mainPos.InternalY && mPos.Z == mainPos.Z)
				{
					pos2main.TryRemove(new Xyz(fromPos.X, fromPos.InternalY, fromPos.Z), out mPos);
					return true;
				}
			}
			return false;
		}
	}
}