#pragma warning disable 0162
#pragma warning disable 0618
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityObject = UnityEngine.Object;
namespace Zios{
	using Events;
	#if UNITY_EDITOR
	using UnityEditor;
	using CallbackFunction = UnityEditor.EditorApplication.CallbackFunction;
	#else
	public delegate void CallbackFunction();
	#endif
	[InitializeOnLoad]
	public static partial class Utility{
		private static float sceneCheck;
		private static Dictionary<object,KeyValuePair<CallbackFunction,float>> delayedMethods = new Dictionary<object,KeyValuePair<CallbackFunction,float>>();
		private static Dictionary<string,Type> internalTypes = new Dictionary<string,Type>();
		static Utility(){Utility.Setup();}
		public static void Setup(){
			Event.Add("On Late Update",(Method)Utility.CheckLoaded);
			Event.Add("On Late Update",(Method)Utility.CheckDelayed);
			#if UNITY_EDITOR
			Event.Register("On Global Event");
			Event.Register("On Editor Update");
			Event.Register("On Prefab Changed");
			Event.Register("On Lightmap Baked");
			Event.Register("On Windows Reordered");
			Event.Register("On Hierarchy Changed");
			Event.Register("On Asset Changed");
			Event.Register("On Asset Saving");
			Event.Register("On Asset Creating");
			Event.Register("On Asset Deleting");
			Event.Register("On Asset Moving");
			Event.Register("On Scene Loaded");
			Event.Register("On Editor Scene Loaded");
			Event.Register("On Mode Changed");
			Event.Register("On Enter Play");
			Event.Register("On Exit Play");
			Event.Register("On Undo Flushing");
			Event.Register("On Undo");
			Event.Register("On Redo");
			#if UNITY_5
			Camera.onPostRender += (Camera camera)=>Event.Call("On Camera Post Render",camera);
			Camera.onPreRender += (Camera camera)=>Event.Call("On Camera Pre Render",camera);
			Camera.onPreCull += (Camera camera)=>Event.Call("On Camera Pre Cull",camera);
			Lightmapping.completed += ()=>Event.Call("On Lightmap Baked");
			#endif
			Undo.willFlushUndoRecord += ()=>Event.Call("On Undo Flushing");
			Undo.undoRedoPerformed += ()=>Event.Call("On Undo");
			Undo.undoRedoPerformed += ()=>Event.Call("On Redo");
			PrefabUtility.prefabInstanceUpdated += (GameObject target)=>Event.Call("On Prefab Changed",target);
			EditorApplication.projectWindowChanged += ()=>Event.Call("On Project Changed");
			EditorApplication.playmodeStateChanged += ()=>Event.Call("On Mode Changed");
			EditorApplication.playmodeStateChanged += ()=>{
				bool changing = EditorApplication.isPlayingOrWillChangePlaymode;
				bool playing = Application.isPlaying;
				if(changing && !playing){Event.Call("On Enter Play");}
				if(!changing && playing){Event.Call("On Exit Play");}
			};
			EditorApplication.hierarchyWindowChanged += ()=>Event.DelayCall("On Hierarchy Changed",0.25f);
			EditorApplication.update += ()=>Event.Call("On Editor Update");
			EditorApplication.update += ()=>Utility.CheckLoaded(true);
			EditorApplication.update += ()=>Utility.CheckDelayed(true);
			CallbackFunction windowEvent = ()=>Event.Call("On Window Reordered");
			CallbackFunction globalEvent = ()=>Event.Call("On Global Event");
			var windowsReordered = typeof(EditorApplication).GetVariable<CallbackFunction>("windowsReordered");
			typeof(EditorApplication).SetVariable("windowsReordered",windowsReordered+windowEvent);
			var globalEventHandler = typeof(EditorApplication).GetVariable<CallbackFunction>("globalEventHandler");
			typeof(EditorApplication).SetVariable("globalEventHandler",globalEventHandler+globalEvent);
			#endif
		}
		public static void CheckLoaded(){Utility.CheckLoaded(false);}
		public static void CheckLoaded(bool editor){
			if(editor && Application.isPlaying){return;}
			if(!editor && !Application.isPlaying){return;}
			if(Time.realtimeSinceStartup < 0.5 && Utility.sceneCheck == 0){
				var term = editor ? " Editor" : "";
				Event.Call("On" + term + " Scene Loaded");
				Utility.sceneCheck = 1;
			}
			if(Time.realtimeSinceStartup > Utility.sceneCheck){
				Utility.sceneCheck = 0;
			}
		}
		public static void CheckDelayed(){Utility.CheckDelayed(false);}
		public static void CheckDelayed(bool editorCheck){
			if(editorCheck && Application.isPlaying){return;}
			if(!editorCheck && !Application.isPlaying){return;}
			if(Utility.delayedMethods.Count < 1){return;}
			foreach(var item in Utility.delayedMethods.Copy()){
				var method = item.Value.Key;
				float callTime = item.Value.Value;
				if(Time.realtimeSinceStartup > callTime){
					method();
					Utility.delayedMethods.Remove(item.Key);
				}
			}
		}
		//============================
		// General
		//============================
		public static void CallEditorPref(string name,bool showWarnings=false){
			#if UNITY_EDITOR
			var callbacks = EditorPrefs.GetString(name);
			var called = new List<string>();
			var success = new List<string>();
			bool debug = ObjectExtension.debug;
			ObjectExtension.debug = showWarnings;
			foreach(var method in callbacks.Split("|")){
				if(called.Contains(method) || method.IsEmpty()){continue;}
				if(!method.CallMethod().IsNull()){
					success.Add(method);	
				}
				called.Add(method);
			}
			ObjectExtension.debug = debug;
			var value = success.Count > 0 ? success.Join("|") : "";
			EditorPrefs.SetString(name,value);
			#endif
		}
		public static void TogglePlayerPref(string name,bool fallback=false){
			bool value = !(PlayerPrefs.GetInt(name) == fallback.ToInt());
			PlayerPrefs.SetInt(name,value.ToInt());
		}
		public static void ToggleEditorPref(string name,bool fallback=false){
			#if UNITY_EDITOR
			bool value = !EditorPrefs.GetBool(name,fallback);
			EditorPrefs.SetBool(name,value);
			#endif
		}
		public static void Destroy(UnityObject target){
			if(target.IsNull()){return;}
			if(target is Component){
				var component = target.As<Component>();
				if(component.gameObject.IsNull()){return;}
			}
			if(!Application.isPlaying){UnityObject.DestroyImmediate(target,true);}
			else{UnityObject.Destroy(target);}
		}
		public static Type GetType(string path){
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			foreach(var assembly in assemblies){
				Type[] types = assembly.GetTypes();
				foreach(Type type in types){
					if(type.FullName == path){
						return type;
					}
				}
			}
			return null;
		}
		public static void ResetTypeCache(){Utility.internalTypes.Clear();}
		public static Type GetUnityType(string name){
			if(Utility.internalTypes.ContainsKey(name)){return Utility.internalTypes[name];}
			#if UNITY_EDITOR
			var fullCheck = name.ContainsAny(".","+");
			var alternative = name.ReplaceLast(".","+");
			var term = alternative.Split("+").Last();
			foreach(var type in typeof(UnityEditor.Editor).Assembly.GetTypes()){
				bool match = fullCheck && (type.FullName.Contains(name) || type.FullName.Contains(alternative)) && term.Matches(type.Name,true);
				if(type.Name == name || match){
					Utility.internalTypes[name] = type;
					return type;
				}
			}
			foreach(var type in typeof(UnityEngine.Object).Assembly.GetTypes()){
				bool match = fullCheck && (type.FullName.Contains(name) || type.FullName.Contains(alternative)) && term.Matches(type.Name,true);
				if(type.Name == name || match){
					Utility.internalTypes[name] = type;
					return type;
				}
			}
			#endif
			return null;
		}
		public static void EditorLog(string text){
			if(!Application.isPlaying){
				Debug.Log(text);
			}
		}
		//============================
		// Callbacks
		//============================
		public static void EditorCall(CallbackFunction method){
			#if UNITY_EDITOR
			if(!Utility.IsPlaying()){
				method();
			}
			#endif
		}
		public static void DelayCall(CallbackFunction method){
			#if UNITY_EDITOR
			if(EditorApplication.delayCall != method){
				EditorApplication.delayCall += method;
			}
			return;
			#endif
			Utility.DelayCall(method,0);
		}
		public static void DelayCall(CallbackFunction method,float seconds){
			Utility.DelayCall(method,method,seconds);
		}
		public static void DelayCall(object key,CallbackFunction method,float seconds){
			if(!key.IsNull() && !method.IsNull()){
				Utility.delayedMethods[key] = new KeyValuePair<CallbackFunction,float>(method,Time.realtimeSinceStartup + seconds);
			}
		}
		public static void RepeatCall(CallbackFunction method,float seconds){
			CallbackFunction repeat = ()=>Utility.DelayCall(()=>Utility.RepeatCall(method,seconds));
			Utility.DelayCall(method+repeat,seconds);
		}
	}
}