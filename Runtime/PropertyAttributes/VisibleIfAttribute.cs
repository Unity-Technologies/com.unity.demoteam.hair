namespace Unity.DemoTeam.Hair
{
	public class VisibleIfAttribute : ComparePropertyBase
	{
		public VisibleIfAttribute(bool fieldFlag) : base("", fieldFlag) { }
		public VisibleIfAttribute(string fieldName, object cmpValue) : base(fieldName, cmpValue) { }
		public VisibleIfAttribute(string fieldName, CompareOp cmpOp, object cmpValue) : base(fieldName, cmpOp, cmpValue) { }
	}
}
