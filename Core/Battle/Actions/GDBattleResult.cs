using Godot;

namespace Cthangover.Core.Battle.Actions
{
	[GlobalClass]
	public partial class GDBattleResult : RefCounted
	{
		public int SourceDamage;
		public int SourceDefence;
		public int TargetDamage;
		public int TargetDefence;
		public bool Result = true;

		public Characters.ChangedAttributes ToChangedAttributes()
		{
			return new Characters.ChangedAttributes
			{
				Source = new Characters.CharacterSet { Damage = SourceDamage, Defence = SourceDefence },
				Target = new Characters.CharacterSet { Damage = TargetDamage, Defence = TargetDefence },
				Result = Result
			};
		}
	}
}
