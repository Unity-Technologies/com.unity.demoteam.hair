namespace Unity.DemoTeam.Hair
{
	public class EditableIfAttribute : ComparePropertyBase
	{
		public EditableIfAttribute(bool fieldFlag) : base("", fieldFlag) { }
		public EditableIfAttribute(string fieldName, object cmpValue) : base(fieldName, cmpValue) { }
		public EditableIfAttribute(string fieldName, CompareOp cmpOp, object cmpValue) : base(fieldName, cmpOp, cmpValue) { }
	}
}
