using System;
using UnityEngine;
using UnityEditor;

namespace Unity.DemoTeam.Hair
{
	public static class ComparePropertyUtility
	{
		public static bool Evaluate(ComparePropertyBase attribute, SerializedProperty property)
		{
			var result = false;
			var attrib = attribute;
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
						case TypeCode.UInt16:
						case TypeCode.UInt32:
							result = Compare(attrib.cmpOp, searchProperty.intValue, (int)((uint)attrib.cmpValue));
							break;

						case TypeCode.UInt64:
							result = Compare(attrib.cmpOp, searchProperty.longValue, (long)((ulong)attrib.cmpValue));
							break;

						case TypeCode.SByte:
						case TypeCode.Int16:
						case TypeCode.Int32:
							result = Compare(attrib.cmpOp, searchProperty.intValue, (int)attrib.cmpValue);
							break;

						case TypeCode.Int64:
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
			else if (attrib.cmpType == TypeCode.Boolean)
			{
				result = (bool)attrib.cmpValue;
			}
			return result;
		}

		static bool Compare<T>(CompareOp op, T a, T b) where T : IComparable<T>
		{
			switch (op)
			{
				case CompareOp.Eq: return a.CompareTo(b) == 0;
				case CompareOp.Geq: return a.CompareTo(b) >= 0;
				case CompareOp.Gt: return a.CompareTo(b) > 0;
				case CompareOp.Leq: return a.CompareTo(b) <= 0;
				case CompareOp.Lt: return a.CompareTo(b) < 0;
				case CompareOp.Neq: return a.CompareTo(b) != 0;
				default: return false;
			}
		}
	}
}
