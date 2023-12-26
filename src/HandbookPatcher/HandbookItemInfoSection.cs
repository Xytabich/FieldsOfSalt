using System;

namespace FieldsOfSalt
{
	public enum HandbookItemInfoSection : int
	{
		BeforeAll,
		AfterAll,
		AfterItemHeader,
		BeforeExtraSections,
		BeforeStorableInfo,
		[Obsolete("Use BeforeStorableInfo or BeforeExtraSections instead")]
		BeforeHandbookInfo = BeforeStorableInfo
	}
}