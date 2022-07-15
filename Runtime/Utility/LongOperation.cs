#define ENABLED

using System;
using UnityEngine;
using UnityEditor;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.DemoTeam.Hair
{
	using Stack = UnsafeList<LongOperationDesc>;

	public struct LongOperationDesc
	{
		public FixedString128 operationTitle;
		public FixedString128 operationStatus;
		public float operationProgress;
		public int operationLastTick;
	}

	public struct LongOperationScope : IDisposable
	{
		public LongOperationDesc desc;

		public LongOperationScope(in FixedString128 operationTitle)
		{
			desc.operationTitle = operationTitle;
			desc.operationStatus = new FixedString128();
			desc.operationProgress = 0.0f;

			unchecked
			{
				// should be safe unless one waits a long time (~24 days) showing first progress
				// https://docs.microsoft.com/en-us/dotnet/api/system.environment.tickcount?view=net-6.0
				desc.operationLastTick = Environment.TickCount - int.MaxValue;
			}

#if ENABLED
			LongOperationStack.Push(desc);
			LongOperationStack.Show();
#endif
		}

		bool WaitForTime()
		{
			const int WAIT_TICKS = 66;
			unchecked
			{
				var tick = Environment.TickCount;
				var tickDiff = tick - desc.operationLastTick;
				if (tickDiff > WAIT_TICKS)
				{
					desc.operationLastTick = tick;
					return false;
				}
				else
				{
					return true;
				}
			}
		}

		public void UpdateStatus(in FixedString128 operationStatus, float operationProgress)
		{
#if ENABLED
			if (WaitForTime())
				return;

			desc.operationStatus = operationStatus;
			desc.operationProgress = operationProgress;

			LongOperationStack.Edit(desc);
			LongOperationStack.Show();
#endif
		}

		public void UpdateStatus(in FixedString128 operationStatus, int operationProgressIndex, int operationProgressCount)
		{
#if ENABLED
			if (WaitForTime())
				return;

			var operationStatusEx = operationStatus;
			{
				operationStatusEx.Append(' ');
				operationStatusEx.Append(operationProgressIndex);
				operationStatusEx.Append(" / ");
				operationStatusEx.Append(operationProgressCount);
			}

			desc.operationStatus = operationStatusEx;
			desc.operationProgress = (float)operationProgressIndex / operationProgressCount;

			LongOperationStack.Edit(desc);
			LongOperationStack.Show();
#endif
		}

		public void Dispose()
		{
#if ENABLED
			LongOperationStack.Pop();
#endif
		}
	}

	public static class LongOperationStack
	{
		private static Stack s_stack;

		public static void Push(in LongOperationDesc headDesc)
		{
			if (s_stack.IsCreated == false)
				s_stack = new Stack(4, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

			s_stack.Add(headDesc);
		}

		public static void Edit(in LongOperationDesc headDesc)
		{
			if (s_stack.IsEmpty)
				return;

			s_stack.ElementAt(s_stack.Length - 1) = headDesc;
		}

		public static void Show()
		{
			if (s_stack.IsEmpty)
				return;

			var concatTitle = new FixedString4096();
			var concatStatus = new FixedString4096();
			var concatProgress = 0.0f;
			{
				var concatTitleHead = -1;
				var concatTitleLimit = 3;
				var concatTitleOffset = 0;
				var concatStatusHead = -1;
				var concatStatusLimit = 4;
				var concatStatusOffset = 0;
				var concatStatusDefault = false;

				void ConditionalCopy(bool cond, ref int limit, int input, ref int output) => output = (cond && limit-- > 0) ? input : output;
				void ConditionalAppend(bool cond, in FixedString128 input, ref FixedString4096 concat)
				{
					if (cond && input.Length > 0)
					{
						if (concat.Length > 0)
							concat.Append(" > ");

						concat.Append(input);
					}
				};

				for (int i = s_stack.Length - 1; i >= 0; i--)
				{
					ref var desc = ref s_stack.ElementAt(i);
					{
						ConditionalCopy(desc.operationTitle.Length > 0, ref concatTitleLimit, i, ref concatTitleOffset);
						ConditionalCopy(desc.operationStatus.Length > 0, ref concatStatusLimit, i, ref concatStatusOffset);

						if (concatTitleHead == -1 && desc.operationTitle.Length > 0)
							concatTitleHead = concatTitleOffset;
						if (concatStatusHead == -1 && desc.operationStatus.Length > 0)
							concatStatusHead = concatStatusOffset;
					}
				}

				if (concatStatusHead < concatTitleHead)
				{
					if (concatStatusLimit <= 0)
						concatStatusOffset++;

					concatStatusDefault = true;
				}

				ConditionalAppend(concatTitleLimit < 0, "..", ref concatTitle);
				ConditionalAppend(concatStatusLimit < 0, "..", ref concatStatus);

				for (int i = 0; i != s_stack.Length; i++)
				{
					ref var desc = ref s_stack.ElementAt(i);
					{
						ConditionalAppend(i >= concatTitleOffset, desc.operationTitle, ref concatTitle);
						ConditionalAppend(i >= concatStatusOffset, desc.operationStatus, ref concatStatus);
						concatProgress = concatProgress + (1.0f - concatProgress) * desc.operationProgress;
					}
				}

				ConditionalAppend(concatStatusDefault, "...", ref concatStatus);
			}

#if UNITY_EDITOR
			EditorUtility.DisplayProgressBar(concatTitle.Value, concatStatus.Value, concatProgress);
#else
			{
				var concatProgressSymbols = new FixedString128();
				var concatProgressSymbolsMax = concatProgressSymbols.Capacity / 4 - 2;
				var concatProgressSymbolsLit = Mathf.RoundToInt(concatProgress * concatProgressSymbolsMax);

				concatProgressSymbols.Append('[');
				{
					for (int i = 0; i != concatProgressSymbolsLit; i++)
						concatProgressSymbols.Append('#');
					for (int i = concatProgressSymbolsLit; i != concatProgressSymbolsMax; i++)
						concatProgressSymbols.Append(' ');
				}
				concatProgressSymbols.Append(']');

				Debug.LogFormat("{0} --- {1} --- {2}", concatTitle.Value, concatStatus.Value, concatProgressSymbols.Value);
			}
#endif
		}

		public static void Hide()
		{
#if UNITY_EDITOR
			EditorUtility.ClearProgressBar();

			//TODO find a way to reset hotcontrol if a mouse-hold-drag initiated the operation and the hold was released in the meantime
			//if (s_stack.IsEmpty)
			//{
			//	EditorGUIUtility.hotControl = 0;
			//}
#endif
		}

		public static void Pop()
		{
			if (s_stack.IsEmpty)
				return;

			s_stack.RemoveAt(s_stack.Length - 1);

			if (s_stack.IsEmpty)
				Hide();
			else
				Show();
		}
	}
}
