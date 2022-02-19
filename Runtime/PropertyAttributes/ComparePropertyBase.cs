using System;
using UnityEngine;

namespace Unity.DemoTeam.Hair
{
	public enum CompareOp
	{
		Eq,
		Geq,
		Gt,
		Leq,
		Lt,
		Neq,
	}

	[AttributeUsage(AttributeTargets.Field)]
	public abstract class ComparePropertyBase : PropertyAttribute
	{
		public readonly string fieldName;
		public readonly object cmpValue;
		public readonly TypeCode cmpType;
		public readonly CompareOp cmpOp;

		public ComparePropertyBase(string fieldName, object cmpValue) : this(fieldName, CompareOp.Eq, cmpValue) { }
		public ComparePropertyBase(string fieldName, CompareOp cmpOp, object cmpValue)
		{
			this.fieldName = fieldName;
			this.cmpValue = cmpValue;
			this.cmpType = cmpValue is null ? TypeCode.Empty : Type.GetTypeCode(cmpValue.GetType());
			this.cmpOp = cmpOp;
		}
	}
}
