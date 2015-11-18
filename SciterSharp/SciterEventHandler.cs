﻿// Copyright 2015 Ramon F. Mendes
//
// This file is part of SciterSharp.
// 
// SciterSharp is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// SciterSharp is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with SciterSharp.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SciterSharp.Interop;

namespace SciterSharp
{
	public abstract class SciterEventHandler
	{
#if DEBUG
		private volatile bool _is_attached = false;
		~SciterEventHandler() { Debug.Assert(_is_attached==false); }
#endif

        private static List<SciterEventHandler> _attached_handlers = new List<SciterEventHandler>();// we keep a copy of all attached instances to guard from GC removal

		public SciterEventHandler() { _proc = EventProc; }
		public readonly SciterXBehaviors.FPTR_ElementEventProc _proc;// keep a copy of the delegate so it survives GC

		// Overridables
		protected virtual void Subscription(SciterElement se, out SciterXBehaviors.EVENT_GROUPS event_groups)
		{
			event_groups = SciterXBehaviors.EVENT_GROUPS.HANDLE_ALL;
		}

		protected virtual void Attached(SciterElement se) {}
		protected virtual void Detached(SciterElement se) {}

		protected virtual bool OnMouse(SciterElement se, SciterXBehaviors.MOUSE_PARAMS prms)	{ return false; }
		protected virtual bool OnKey(SciterElement se, SciterXBehaviors.KEY_PARAMS prms)		{ return false; }
		protected virtual bool OnFocus(SciterElement se, SciterXBehaviors.FOCUS_PARAMS prms)	{ return false; }
		protected virtual bool OnDraw(SciterElement se, SciterXBehaviors.DRAW_PARAMS prms)		{ return false; }

		protected virtual bool OnTimer(SciterElement se)										{ return false; }
		protected virtual bool OnTimer(SciterElement se, IntPtr extTimerId)						{ return false; }
		protected virtual bool OnSize(SciterElement se)											{ return false; }

		protected virtual bool OnMethodCall(SciterElement se, uint methodID)					{ return false; }
		protected virtual bool OnScriptCall(SciterElement se, string name, SciterValue[] args, out SciterValue result) { result = null; return false; }

		protected virtual bool OnEvent(SciterElement elSource, SciterElement elTarget, SciterXBehaviors.BEHAVIOR_EVENTS type, IntPtr reason, SciterValue data)	{ return false; }

		protected virtual bool OnDataArrived(SciterElement se, SciterXBehaviors.DATA_ARRIVED_PARAMS prms)	{ return false; }

		protected virtual bool OnScroll(SciterElement se, SciterXBehaviors.SCROLL_PARAMS prms)		{ return false; }
		protected virtual bool OnGesturel(SciterElement se, SciterXBehaviors.GESTURE_PARAMS prms)	{ return false; }

		// EventProc
		private bool EventProc(IntPtr tag, IntPtr he, uint evtg, IntPtr prms)
		{
			SciterElement se = new SciterElement(he);
			switch((SciterXBehaviors.EVENT_GROUPS) evtg)
			{
				case SciterXBehaviors.EVENT_GROUPS.SUBSCRIPTIONS_REQUEST:
					{
						SciterXBehaviors.EVENT_GROUPS groups;
						Subscription(se, out groups);
						Marshal.WriteInt32(prms, (int) groups);
						return true;
					}

				case SciterXBehaviors.EVENT_GROUPS.HANDLE_INITIALIZATION:
					{
						SciterXBehaviors.INITIALIZATION_PARAMS p = (SciterXBehaviors.INITIALIZATION_PARAMS) Marshal.PtrToStructure(prms, typeof(SciterXBehaviors.INITIALIZATION_PARAMS));
						if(p.cmd == SciterXBehaviors.INITIALIZATION_EVENTS.BEHAVIOR_ATTACH)
						{
#if DEBUG
                            Debug.Assert(_is_attached==false);
                            _is_attached = true;
#endif
                            _attached_handlers.Add(this);
							Attached(se);
						}
						else if(p.cmd == SciterXBehaviors.INITIALIZATION_EVENTS.BEHAVIOR_DETACH)
						{
#if DEBUG
                            Debug.Assert(_is_attached==true);
                            _is_attached = false;
#endif
							_attached_handlers.Remove(this);
							Detached(se);
						}
						return true;
					}

				case SciterXBehaviors.EVENT_GROUPS.HANDLE_MOUSE:
					{
						SciterXBehaviors.MOUSE_PARAMS p = (SciterXBehaviors.MOUSE_PARAMS) Marshal.PtrToStructure(prms, typeof(SciterXBehaviors.MOUSE_PARAMS));
						return OnMouse(se, p);
					}

				case SciterXBehaviors.EVENT_GROUPS.HANDLE_KEY:
					{
						SciterXBehaviors.KEY_PARAMS p = (SciterXBehaviors.KEY_PARAMS) Marshal.PtrToStructure(prms, typeof(SciterXBehaviors.KEY_PARAMS));
						return OnKey(se, p);
					}

				case SciterXBehaviors.EVENT_GROUPS.HANDLE_FOCUS:
					{
						SciterXBehaviors.FOCUS_PARAMS p = (SciterXBehaviors.FOCUS_PARAMS) Marshal.PtrToStructure(prms, typeof(SciterXBehaviors.FOCUS_PARAMS));
						return OnFocus(se, p);
					}

				case SciterXBehaviors.EVENT_GROUPS.HANDLE_TIMER:
					{
						SciterXBehaviors.TIMER_PARAMS p = (SciterXBehaviors.TIMER_PARAMS) Marshal.PtrToStructure(prms, typeof(SciterXBehaviors.TIMER_PARAMS));
						if(p.timerId!=IntPtr.Zero)
							return OnTimer(se, p.timerId);
						return OnTimer(se);
					}

				case SciterXBehaviors.EVENT_GROUPS.HANDLE_BEHAVIOR_EVENT:
					{
						SciterXBehaviors.BEHAVIOR_EVENT_PARAMS p = (SciterXBehaviors.BEHAVIOR_EVENT_PARAMS) Marshal.PtrToStructure(prms, typeof(SciterXBehaviors.BEHAVIOR_EVENT_PARAMS));
						return OnEvent(new SciterElement(p.he), se, (SciterXBehaviors.BEHAVIOR_EVENTS) p.cmd, p.reason, new SciterValue(p.data));// maybe I should not pass SciterValue to avoid add-refing the VALUE
					}

				case SciterXBehaviors.EVENT_GROUPS.HANDLE_METHOD_CALL:
					{
						SciterXDom.METHOD_PARAMS p = (SciterXDom.METHOD_PARAMS) Marshal.PtrToStructure(prms, typeof(SciterXDom.METHOD_PARAMS));
						return OnMethodCall(se, p.methodID);
					}

				case SciterXBehaviors.EVENT_GROUPS.HANDLE_DATA_ARRIVED:
					{
						SciterXBehaviors.DATA_ARRIVED_PARAMS p = (SciterXBehaviors.DATA_ARRIVED_PARAMS) Marshal.PtrToStructure(prms, typeof(SciterXBehaviors.DATA_ARRIVED_PARAMS));
						return OnDataArrived(se, p);
					}

				case SciterXBehaviors.EVENT_GROUPS.HANDLE_SCROLL:
					{
						SciterXBehaviors.SCROLL_PARAMS p = (SciterXBehaviors.SCROLL_PARAMS) Marshal.PtrToStructure(prms, typeof(SciterXBehaviors.SCROLL_PARAMS));
						return OnScroll(se, p);
					}

				case SciterXBehaviors.EVENT_GROUPS.HANDLE_SIZE:
					return OnSize(se);

				case SciterXBehaviors.EVENT_GROUPS.HANDLE_SCRIPTING_METHOD_CALL:
					{
						IntPtr RESULT_OFFSET = Marshal.OffsetOf(typeof(SciterXBehaviors.SCRIPTING_METHOD_PARAMS), "result");
						if(IntPtr.Size==4)
							Debug.Assert(RESULT_OFFSET.ToInt32()==16);
						else if(IntPtr.Size==8)
							Debug.Assert(RESULT_OFFSET.ToInt32()==24);

						SciterXBehaviors.SCRIPTING_METHOD_PARAMS p = (SciterXBehaviors.SCRIPTING_METHOD_PARAMS) Marshal.PtrToStructure(prms, typeof(SciterXBehaviors.SCRIPTING_METHOD_PARAMS));
						SciterXBehaviors.SCRIPTING_METHOD_PARAMS_Wraper pw = new SciterXBehaviors.SCRIPTING_METHOD_PARAMS_Wraper(p);

						bool bOK = OnScriptCall(se, pw.name, pw.args, out pw.result);
                        if(bOK && pw.result!=null)
                        {
						    SciterXValue.VALUE vres = pw.result.ToVALUE();
						    IntPtr vptr = IntPtr.Add(prms, RESULT_OFFSET.ToInt32());
						    Marshal.StructureToPtr(vres, vptr, false);
                        }
						
						return bOK;
					}

				case SciterXBehaviors.EVENT_GROUPS.HANDLE_TISCRIPT_METHOD_CALL:
					/*
					COMMENTED BECAUSE THIS EVENT IS NEVER USED, AND JUST ADDS MORE CONFUSION
					BETTER USE EVENT_GROUPS.HANDLE_SCRIPTING_METHOD_CALL INSTEAD
						{
							SciterXBehaviors.TISCRIPT_METHOD_PARAMS p = Marshal.PtrToStructure<SciterXBehaviors.TISCRIPT_METHOD_PARAMS>(prms);
							bool res = OnScriptCall(se, p);
							return res;
						}
					*/
					return false;  
					
				case SciterXBehaviors.EVENT_GROUPS.HANDLE_GESTURE:
					{
						SciterXBehaviors.GESTURE_PARAMS p = (SciterXBehaviors.GESTURE_PARAMS) Marshal.PtrToStructure(prms, typeof(SciterXBehaviors.GESTURE_PARAMS));
						return OnGesturel(se, p);
					}
					
				default:
					Debug.Assert(false);
					return false;
			}
		}
	}
}