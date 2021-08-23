using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.DemoTeam.Hair
{
	public enum CmpOp
	{
		Eq,
		Geq,
		Gt,
		Leq,
		Lt,
		Neq,
	}

	[AttributeUsage(AttributeTargets.Field)]
	public abstract class CmpFieldAttribute : PropertyAttribute
	{
		public readonly string fieldName;
		public readonly object cmpValue;
		public readonly TypeCode cmpType;
		public readonly CmpOp cmpOp;

		public CmpFieldAttribute(string fieldName, object cmpValue) : this(fieldName, CmpOp.Eq, cmpValue) { }
		public CmpFieldAttribute(string fieldName, CmpOp cmpOp, object cmpValue)
		{
			this.fieldName = fieldName;
			this.cmpValue = cmpValue;
			this.cmpType = cmpValue is null ? TypeCode.Empty : Type.GetTypeCode(cmpValue.GetType());
			this.cmpOp = cmpOp;
		}
	}

#if UNITY_EDITOR
	public abstract class CmpFieldAttributeDrawer : PropertyDrawer
	{
		private static bool Compare<T>(CmpOp op, T a, T b) where T : IComparable<T>
		{
			switch (op)
			{
				case CmpOp.Eq: return a.CompareTo(b) == 0;
				case CmpOp.Geq: return a.CompareTo(b) >= 0;
				case CmpOp.Gt: return a.CompareTo(b) > 0;
				case CmpOp.Leq: return a.CompareTo(b) <= 0;
				case CmpOp.Lt: return a.CompareTo(b) < 0;
				case CmpOp.Neq: return a.CompareTo(b) != 0;
				default: return false;
			}
		}

		protected bool Compare(SerializedProperty property)
		{
			var result = false;
			var attrib = (CmpFieldAttribute)base.attribute;
			if (attrib.fieldName.Length > 0)
			{
				SerializedProperty searchProperty;

				var searchPath = property.propertyPath;
				int searchPathDelim = searchPath.LastIndexOf('.');
				if (searchPathDelim == -1)
				{
					searchProperty = property.serializedObject.FindProperty(attrib.fieldName);
				}
				else
				{
					searchProperty = property.serializedObject.FindProperty(searchPath.Substring(0, searchPathDelim)).FindPropertyRelative(attrib.fieldName);
				}

				if (searchProperty != null)
				{
					switch (attrib.cmpType)
					{
						case TypeCode.Boolean:
							if (searchProperty.propertyType == SerializedPropertyType.ObjectReference)
								result = Compare(attrib.cmpOp, searchProperty.objectReferenceValue != null, (bool)attrib.cmpValue);
							else
								result = Compare(attrib.cmpOp, searchProperty.boolValue, (bool)attrib.cmpValue);
							break;

						case TypeCode.Byte:
						case TypeCode.SByte:
						case TypeCode.Int16:
						case TypeCode.Int32:
						case TypeCode.UInt16:
						case TypeCode.UInt32:
							result = Compare(attrib.cmpOp, searchProperty.intValue, (int)attrib.cmpValue);
							break;

						case TypeCode.Int64:
						case TypeCode.UInt64:
							result = Compare(attrib.cmpOp, searchProperty.longValue, (long)attrib.cmpValue);
							break;

						case TypeCode.Single:
							result = Compare(attrib.cmpOp, searchProperty.floatValue, (float)attrib.cmpValue);
							break;

						case TypeCode.Double:
							result = Compare(attrib.cmpOp, searchProperty.doubleValue, (double)attrib.cmpValue);
							break;

						case TypeCode.Empty:
							result = Compare(attrib.cmpOp, searchProperty.objectReferenceValue != null, true);
							break;

						default:
							//TODO add the remaining
							break;
					}
				}
			}
			return result;
		}
	}
#endif
}
