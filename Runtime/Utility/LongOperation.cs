#define ENABLED

using System;
using UnityEngine;
using UnityEditor;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.DemoTeam.Hair
{
#if !HAS_PACKAGE_UNITY_COLLECTIONS_1_0_0_PRE_5
	using FixedString128Bytes = FixedString128;
	using FixedString4096Bytes = FixedString4096;
#endif

	using Stack = UnsafeList<LongOperationDesc>;

	public struct LongOperationDesc
	{
		public FixedString128Bytes operationTitle;
		public FixedString128Bytes operationStatus;
		public float operationProgress;
		public int operationLastTick;
	}

	public struct LongOperationScope : IDisposable
	{
		public LongOperationDesc desc;

		public LongOperationScope(in FixedString128Bytes operationTitle)
		{
			desc.operationTitle = operationTitle;
			desc.operationStatus = new FixedString128Bytes();
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
			const int WAIT_TICKS = 200;// milliseconds
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

		public void UpdateStatus(in FixedString128Bytes operationStatus, float operationProgress)
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

		public void UpdateStatus(in FixedString128Bytes operationStatus, int operationProgressIndex, int operationProgressCount)
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
			if (s_stack.IsEmpty || LongOperationOutput.s_enable == false)
				return;

			var concatTitle = new FixedString4096Bytes();
			var concatStatus = new FixedString4096Bytes();
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
				void ConditionalAppend(bool cond, in FixedString128Bytes input, ref FixedString4096Bytes concat)
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

				/*
				var j = s_stack.Length - 1;
				if (j > 0)
				{
					var progressBase = s_stack.ElementAt(j - 1).operationProgress;
					var progressHead = s_stack.ElementAt(j).operationProgress;
					concatProgress = progressBase + (1.0f - progressBase) * progressHead;
				}
				else
				{
					concatProgress = s_stack.ElementAt(j).operationProgress;
				}
				*/

				ConditionalAppend(concatStatusDefault, "...", ref concatStatus);
			}

			LongOperationOutput.s_output.show(concatTitle.Value, concatStatus.Value, concatProgress);
		}

		public static void Hide()
		{
			if (LongOperationOutput.s_enable == false)
				return;

			LongOperationOutput.s_output.hide();
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

		public static bool IsEmpty()
		{
			return s_stack.IsEmpty;
		}
	}

	public struct LongOperationOutput : IDisposable
	{
		public struct Impl
		{
			public delegate void FnShow(string title, string status, float progress);
			public delegate void FnHide();

			public FnShow show;
			public FnHide hide;
		}

#if UNITY_EDITOR
		public static readonly Impl defaultImplEditor = new Impl
		{
			show = EditorUtility.DisplayProgressBar,
			hide = EditorUtility.ClearProgressBar,
		};
#endif

		public static readonly Impl defaultImplPlayer = new Impl
		{
			show = (title, status, progress) =>
			{
				var concatProgressSymbols = new FixedString128Bytes();
				var concatProgressSymbolsMax = concatProgressSymbols.Capacity / 4 - 2;
				var concatProgressSymbolsLit = Mathf.RoundToInt(progress * concatProgressSymbolsMax);

				concatProgressSymbols.Append('[');
				{
					for (int i = 0; i != concatProgressSymbolsLit; i++)
						concatProgressSymbols.Append('#');
					for (int i = concatProgressSymbolsLit; i != concatProgressSymbolsMax; i++)
						concatProgressSymbols.Append(' ');
				}
				concatProgressSymbols.Append(']');

				Debug.Log(string.Format("{0} --- {1} --- {2}", title, status, concatProgressSymbols.Value));
			},
			hide = () => { },
		};

		public static bool s_enable = false;
#if UNITY_EDITOR
		public static Impl s_output = defaultImplEditor;
#else
		public static Impl s_output = defaultImplPlayer;
#endif

		private static bool s_enablePrev;
		private static Impl s_outputPrev;

		public LongOperationOutput(bool enable) : this(enable, s_output) { }
		public LongOperationOutput(Impl output) : this(s_enable, output) { }
		public LongOperationOutput(bool enable, Impl output)
		{
			s_enablePrev = s_enable;
			s_outputPrev = s_output;

			s_enable = enable;
			s_output = output;
		}

		public void Dispose()
		{
			s_enable = s_enablePrev;
			s_output = s_outputPrev;
		}
	}
}
