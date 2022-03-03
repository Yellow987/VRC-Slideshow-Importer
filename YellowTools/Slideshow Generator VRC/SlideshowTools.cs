#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VRC.SDK3.Avatars.Components;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.ScriptableObjects;
using System;
using System.Linq;
using Object = UnityEngine.Object;

namespace YellowTools
{
    public class SlideshowGeneratorVRC : EditorWindow
    {
        public static VRCAvatarDescriptor myAvatar;
        public static Texture mySlideImages; 
        public static int myNumSlides = 3;
        public static GameObject mySlideParent;
        private static string folderPath;

        private static Color originalGUIColor;

        private static GUIContent iconTrash;
        private static GUIContent iconError;

        private static Vector2 scroll;

        private static string assetPath;

        public static bool useSameParameter;
        public static bool useCustomTree;
        public static bool addTracking;

        public static BlendTree customTree;

        public static bool RightArm, LeftArm, RightLeg, LeftLeg;

        [MenuItem("YellowTools/Slideshow Genereator VRC", false, 150)]
        public static void showWindow()
        {
            GetWindow<SlideshowGeneratorVRC>(false, "Slideshow Genereator VRC", true);
        }

        public void OnGUI()
        {
            
            bool isValid = true;
            originalGUIColor = GUI.backgroundColor;
            scroll = EditorGUILayout.BeginScrollView(scroll);

            using (new GUILayout.VerticalScope("box")){
                myAvatar = (VRCAvatarDescriptor)EditorGUILayout.ObjectField(new GUIContent("Avatar Descriptor", "Drag and Drop your avatar here."), myAvatar, typeof(VRCAvatarDescriptor), true);
                mySlideImages = (Texture)EditorGUILayout.ObjectField(new GUIContent("Slide Images png", "Drag and Drop your image here."), mySlideImages, typeof(Texture), true);
                myNumSlides = (int)EditorGUILayout.IntField("Number of slides", myNumSlides);
                mySlideParent = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Obj to attach slide to.", "Drag and Drop GameObject here."), mySlideParent, typeof(GameObject), true);
                if (!mySlideParent || myNumSlides < 2 || !mySlideImages || !myAvatar)
                    isValid = false;
            }

            int cost = 3;
            int freeMemory = 0;
            if (myAvatar)
            {
                freeMemory = 128;
                if (myAvatar.expressionParameters)
                    freeMemory -= myAvatar.expressionParameters.CalcTotalCost();
            }

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(string.Format("Required Memory: {0}/{1}", cost, freeMemory));
                if (cost > freeMemory)
                {
                    GUILayout.Label(iconError, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    isValid = false;
                }
                GUILayout.FlexibleSpace();
            }

            using (new GUILayout.HorizontalScope())
            {
                EditorGUI.BeginDisabledGroup(!isValid);
                if (GUILayout.Button("Add Slideshow"))
                {
                    addSlideshowVRC();
                }
                EditorGUI.EndDisabledGroup();
            }

            GUILayout.Label("", GUI.skin.horizontalSlider);
            AssetFolderPath(ref assetPath, "Generated Assets", "YellowToolsPath");
            Credit();
            EditorGUILayout.EndScrollView();
        }

        private void addSlideshowVRC(){
            myAvatar.customExpressions = true;
            myAvatar.customizeAnimationLayers = true;

            //set path for assets
            assetPath = PlayerPrefs.GetString("YellowToolsPath", "Assets/YellowTools/Slideshow Generator VRC/Generated Assets");
            ReadyPath(assetPath);
            folderPath = AssetDatabase.GenerateUniqueAssetPath(assetPath + "/" + myAvatar.gameObject.name);
            AssetDatabase.CreateFolder(assetPath, myAvatar.gameObject.name);

            //Dup the FX controller
            AnimatorController myFX = GetPlayableLayer(myAvatar, VRCAvatarDescriptor.AnimLayerType.FX);
            if (!myFX){
                EditorUtility.DisplayDialog("Error", "Create a FX layer", "ok", "cancel");
                return;
            }
            myFX = CopyAssetAndReturn<AnimatorController>(myFX, folderPath + "/" + myFX.name + ".controller");
            SetPlayableLayer(myAvatar, VRCAvatarDescriptor.AnimLayerType.FX, myFX);

            //Duplicate VRC Menu and parameters
            VRCExpressionsMenu myMenu = myAvatar.expressionsMenu;
            VRCExpressionParameters myParams = myAvatar.expressionParameters;
            if (!myMenu){
                EditorUtility.DisplayDialog("Error", "Create a VRC menu", "ok", "cancel");
                return;
            }
            if (!myParams){
                EditorUtility.DisplayDialog("Error", "Create a VRC params", "ok", "cancel");
                return;
            }
            myMenu = CopyAssetAndReturn<VRCExpressionsMenu>(myMenu, folderPath + "/" + myMenu.name + ".asset");
            myParams = CopyAssetAndReturn<VRCExpressionParameters>(myParams, folderPath + "/" + myParams.name + ".asset");
            myAvatar.expressionsMenu = myMenu;
            myAvatar.expressionParameters = myParams;
            
            AddParameters(myParams, new List<VRCExpressionParameters.Parameter>() {
            new VRCExpressionParameters.Parameter(){ name = "nextSlide", saved = false,  valueType = VRCExpressionParameters.ValueType.Bool },
            new VRCExpressionParameters.Parameter(){ name = "prevSlide", saved = false,   valueType = VRCExpressionParameters.ValueType.Bool },
            new VRCExpressionParameters.Parameter(){ name = "slideIsOn?", saved = false,   valueType = VRCExpressionParameters.ValueType.Bool }
            });

            AddControls(myMenu, new List<VRCExpressionsMenu.Control>() { new VRCExpressionsMenu.Control() { name = "Slideshow", type = VRCExpressionsMenu.Control.ControlType.SubMenu, subMenu = Resources.Load<VRCExpressionsMenu>("slideMenu"), icon = Resources.Load<Texture2D>("LC_ToggleOn") } });


            addSlideshow(myAvatar.gameObject, mySlideParent, mySlideImages, myNumSlides, folderPath, myFX);
        }

        private void addSlideshow(GameObject animationParent, GameObject slideParent, Texture image, int numSlides, string folderPath, AnimatorController animatorController){

            //Spawn gameObj under parent with material
            GameObject slide = Resources.Load<GameObject>("Slides");
            slide = CopyAssetAndReturn<GameObject>(slide, folderPath + "/slides.asset");
            Material material = Resources.Load<Material>("Example_Material");
            material = CopyAssetAndReturn<Material>(material, folderPath + "/slideMaterial.asset");
            material.SetTexture("_MainTex", mySlideImages);
            material.name = "slideMaterial";
            slide.GetComponent<MeshRenderer>().material = material;
            slide = Instantiate(slide, slideParent.transform);
            slide.SetActive(false);

            //Create slide transition Animations and transitions for state machine.
            AssetDatabase.CreateFolder(folderPath, "Animations");
            AnimatorControllerLayer layer = new AnimatorControllerLayer();
            layer.name = "SlideTransitions";
            layer.defaultWeight = 1;
            layer.stateMachine = new AnimatorStateMachine();

            AnimatorState prevState = null;
            string animPath = AnimationUtility.CalculateTransformPath(slide.transform, animationParent.transform);
            AnimationCurve curveZero = new AnimationCurve() { keys = new Keyframe[] { new Keyframe(0, 0)}};
            AnimationCurve curveOne = new AnimationCurve() { keys = new Keyframe[] { new Keyframe(0, 1)}};
            for (int i = 0; i < numSlides; i++){
                AnimationClip clip = new AnimationClip();
                AnimationCurve curvew = new AnimationCurve() { keys = new Keyframe[] { new Keyframe(0, (-(float)i/(float)numSlides + (1 - (float)3/(float)numSlides) ))}};
                AnimationCurve curvey = new AnimationCurve() { keys = new Keyframe[] { new Keyframe(0, ((float)3/(float)numSlides))}}; 
                clip.SetCurve(animPath, typeof(MeshRenderer), "material._MainTex_ST.x", curveOne);
                clip.SetCurve(animPath, typeof(MeshRenderer), "material._MainTex_ST.y", curvey);
                clip.SetCurve(animPath, typeof(MeshRenderer), "material._MainTex_ST.z", curveZero);
                clip.SetCurve(animPath, typeof(MeshRenderer), "material._MainTex_ST.w", curvew);
                AssetDatabase.CreateAsset(clip, folderPath + "/Animations/slide" + (i + 1) + ".anim");

                AnimatorState state = layer.stateMachine.AddState("slide" + (i + 1));
                state.motion = clip;
                if (prevState){
                    AnimatorStateTransition prevSlideTransition = state.AddTransition(prevState, true);
                    prevSlideTransition.exitTime = 0.15f;
                    prevSlideTransition.AddCondition(AnimatorConditionMode.If, 1f, "prevSlide");
                    AnimatorStateTransition nextSlideTransition = prevState.AddTransition(state, true); 
                    nextSlideTransition.exitTime = 0.15f;
                    nextSlideTransition.AddCondition(AnimatorConditionMode.If, 1f, "nextSlide");
                }
                prevState = state;
            }
            animatorController.AddLayer(layer);

            //Add parameters 
            animatorController.AddParameter("nextSlide", AnimatorControllerParameterType.Bool);
            animatorController.AddParameter("prevSlide", AnimatorControllerParameterType.Bool);
            animatorController.AddParameter("slideIsOn?", AnimatorControllerParameterType.Bool);

            //Add on off toggle for slide
            layer = new AnimatorControllerLayer();
            layer.name = "slideIsOn?";
            layer.defaultWeight = 1;
            layer.stateMachine = new AnimatorStateMachine();

            AnimationClip offClip = new AnimationClip();
            offClip.SetCurve(animPath, typeof(GameObject), "m_IsActive", new AnimationCurve { keys = new Keyframe[] { new Keyframe { time = 0, value = 0}, new Keyframe { time = 1, value = 0}}});
            AssetDatabase.CreateAsset(offClip, folderPath + "/Animations/slideIsOff.anim");
            AnimatorState offState = layer.stateMachine.AddState("slideIsOff");
            offState.motion = offClip;

            AnimationClip onClip = new AnimationClip();
            onClip.SetCurve(animPath, typeof(GameObject), "m_IsActive", new AnimationCurve { keys = new Keyframe[] { new Keyframe { time = 0, value = 1}, new Keyframe { time = 1, value = 1}}});
            AssetDatabase.CreateAsset(onClip, folderPath + "/Animations/slideIsOn.anim");
            AnimatorState onState = layer.stateMachine.AddState("slideIsOn");
            onState.motion = onClip;

            AnimatorStateTransition onToOff = onState.AddTransition(offState, false); 
            onToOff.AddCondition(AnimatorConditionMode.IfNot, 0, "slideIsOn?");
            AnimatorStateTransition offToOn = offState.AddTransition(onState, false);
            offToOn.AddCondition(AnimatorConditionMode.If, 0, "slideIsOn?");

            animatorController.AddLayer(layer);
        }

        public void AddControl(AvatarMask mask, BlendTree tree, string parameter, string treeParameter1, string treeParameter2)
        {
            myAvatar.customExpressions = true;
            myAvatar.customizeAnimationLayers = true;

            VRCExpressionsMenu myMenu = myAvatar.expressionsMenu;
            VRCExpressionParameters myParams = myAvatar.expressionParameters;

            if (!myMenu)
                myMenu = ReplaceMenu(myAvatar, folderPath);

            if (!myParams)
                myParams = ReplaceParameters(myAvatar, folderPath);

            

            VRCExpressionsMenu mainMenu = myMenu.controls.Find(c => c.name == "Limb Control" && c.type == VRCExpressionsMenu.Control.ControlType.SubMenu && c.subMenu)?.subMenu;

            if (mainMenu == null)
            {
                mainMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                AddControls(myMenu, new List<VRCExpressionsMenu.Control>() { new VRCExpressionsMenu.Control() { name = "Limb Control", type = VRCExpressionsMenu.Control.ControlType.SubMenu, subMenu = mainMenu, icon = Resources.Load<Texture2D>("Icons/LC_HandWaving") } });
                AssetDatabase.CreateAsset(mainMenu, folderPath + "/LC_LimbControlMainMenu.asset");
            }

            VRCExpressionsMenu mySubmenu = Resources.Load<VRCExpressionsMenu>("LC_LimbControlMenu");
            mySubmenu = CopyAssetAndReturn<VRCExpressionsMenu>(mySubmenu, folderPath + "/" + parameter + " Control Menu.asset");

            mySubmenu.controls[0].value = 1;
            mySubmenu.controls[0].parameter = new VRCExpressionsMenu.Control.Parameter() { name = parameter };

            VRCExpressionsMenu.Control.Parameter[] subParameters = new VRCExpressionsMenu.Control.Parameter[2];
            subParameters[0] = new VRCExpressionsMenu.Control.Parameter { name = treeParameter1 };
            subParameters[1] = new VRCExpressionsMenu.Control.Parameter() { name = treeParameter2 };

            mySubmenu.controls[1].subParameters = subParameters;

            EditorUtility.SetDirty(mySubmenu);

            VRCExpressionsMenu.Control newControl = new VRCExpressionsMenu.Control
            {
                type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                name = parameter.Substring(3, parameter.Length - 3),
                subMenu = mySubmenu

            };
            AddControls(mainMenu, new List<VRCExpressionsMenu.Control>() { newControl });

            AddParameters(myParams, new List<VRCExpressionParameters.Parameter>() {
            new VRCExpressionParameters.Parameter(){ name = parameter,      saved = false,  valueType = VRCExpressionParameters.ValueType.Bool, defaultValue=0 },
            new VRCExpressionParameters.Parameter(){ name = treeParameter1, saved = true,   valueType = VRCExpressionParameters.ValueType.Float },
            new VRCExpressionParameters.Parameter(){ name = treeParameter2, saved = true,   valueType = VRCExpressionParameters.ValueType.Float }
            });
        
            BlendTree myTree = CopyAssetAndReturn<BlendTree>(tree, AssetDatabase.GenerateUniqueAssetPath(folderPath + "/" + tree.name + ".blendtree"));
            myTree.blendParameter = treeParameter1;
            myTree.blendParameterY = treeParameter2;
            EditorUtility.SetDirty(myTree);

            void addLimbLayer(AnimatorController controller, bool setTracking = false)
            {

                void LCAddParameter(string s, AnimatorControllerParameterType t)
                {
                    ReadyParameter(controller, s, t);
                }

                LCAddParameter(parameter, AnimatorControllerParameterType.Bool);
                LCAddParameter(treeParameter1, AnimatorControllerParameterType.Float);
                LCAddParameter(treeParameter2, AnimatorControllerParameterType.Float);


                AnimatorControllerLayer newLayer = AddLayer(controller, parameter, 1, mask); 
                AddTag(newLayer, "Limb Control");

                AnimatorState firstState = newLayer.stateMachine.AddState("Idle");
                AnimatorState secondState = newLayer.stateMachine.AddState("Control");

                secondState.motion = myTree;

                if (setTracking)
                {
                    VRCAnimatorTrackingControl trackingControl = firstState.AddStateMachineBehaviour<VRCAnimatorTrackingControl>();
                    VRCAnimatorTrackingControl trackingControl2 = secondState.AddStateMachineBehaviour<VRCAnimatorTrackingControl>();

                    if (mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Head))
                    {
                        trackingControl.trackingHead = VRCAnimatorTrackingControl.TrackingType.Tracking;
                        trackingControl2.trackingHead = VRCAnimatorTrackingControl.TrackingType.Animation;
                    }

                    if (mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Body))
                    {
                        trackingControl.trackingHip = VRCAnimatorTrackingControl.TrackingType.Tracking;
                        trackingControl2.trackingHip = VRCAnimatorTrackingControl.TrackingType.Animation;
                    }

                    if (mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm))
                    {
                        trackingControl.trackingRightHand = VRCAnimatorTrackingControl.TrackingType.Tracking;
                        trackingControl2.trackingRightHand = VRCAnimatorTrackingControl.TrackingType.Animation;
                    }

                    if (mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm))
                    {
                        trackingControl.trackingLeftHand = VRCAnimatorTrackingControl.TrackingType.Tracking;
                        trackingControl2.trackingLeftHand = VRCAnimatorTrackingControl.TrackingType.Animation;
                    }

                    if (mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers))
                    {
                        trackingControl.trackingRightFingers = VRCAnimatorTrackingControl.TrackingType.Tracking;
                        trackingControl2.trackingRightFingers = VRCAnimatorTrackingControl.TrackingType.Animation;
                    }

                    if (mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers))
                    {
                        trackingControl.trackingLeftFingers = VRCAnimatorTrackingControl.TrackingType.Tracking;
                        trackingControl2.trackingLeftFingers = VRCAnimatorTrackingControl.TrackingType.Animation;
                    }

                    if (mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg))
                    {
                        trackingControl.trackingRightFoot = VRCAnimatorTrackingControl.TrackingType.Tracking;
                        trackingControl2.trackingRightFoot = VRCAnimatorTrackingControl.TrackingType.Animation;
                    }

                    if (mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg))
                    {
                        trackingControl.trackingLeftFoot = VRCAnimatorTrackingControl.TrackingType.Tracking;
                        trackingControl2.trackingLeftFoot = VRCAnimatorTrackingControl.TrackingType.Animation;
                    }

                }

                void SetSettings(AnimatorStateTransition t, bool status)
                {
                    t.hasExitTime = false;
                    t.duration = 0.15f;
                    t.AddCondition(status ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, parameter);
                }

                SetSettings(firstState.AddTransition(secondState), true);
                SetSettings(secondState.AddTransition(firstState), false);
            }


            AnimatorController myLocomotion = GetPlayableLayer(myAvatar, VRCAvatarDescriptor.AnimLayerType.Base);
            addLimbLayer(myLocomotion, true);

            AnimatorController myAction = GetPlayableLayer(myAvatar, VRCAvatarDescriptor.AnimLayerType.Action);
            addLimbLayer(myAction);

            AnimatorController mySitting = GetPlayableLayer(myAvatar, VRCAvatarDescriptor.AnimLayerType.Sitting);
            addLimbLayer(mySitting);

            EditorUtility.SetDirty(myAvatar);
        }

        private void AddTracking(AnimatorController c)
        {
            ReadyParameter(c, "LC_Tracking Control", AnimatorControllerParameterType.Int);

            VRCExpressionsMenu myMenu = myAvatar.expressionsMenu;
            VRCExpressionParameters myParams = myAvatar.expressionParameters;

            if (!myMenu)
                myMenu = ReplaceMenu(myAvatar, folderPath);

            if (!myParams)
                myParams = ReplaceParameters(myAvatar, folderPath);

            VRCExpressionsMenu trackingMenu = Resources.Load<VRCExpressionsMenu>("LC_TrackingMainMenu");
            trackingMenu = ReplaceMenu(trackingMenu, folderPath, true);

            AddControls(myMenu, new List<VRCExpressionsMenu.Control>() { new VRCExpressionsMenu.Control() {name="Tracking Control", type= VRCExpressionsMenu.Control.ControlType.SubMenu, subMenu = trackingMenu, icon = Resources.Load<Texture2D>("Icons/LC_RnR") } });
            AddParameters(myParams, new List<VRCExpressionParameters.Parameter>() { new VRCExpressionParameters.Parameter() { name = "LC_Tracking Control", saved = false, valueType = VRCExpressionParameters.ValueType.Int } });

            AnimatorControllerLayer newLayer = AddLayer(c, "LC_Tracking Control", 0);
            AddTag(newLayer, "Limb Control");
            AnimatorStateMachine m = newLayer.stateMachine;
            AnimatorState HoldState = m.AddState("Hold");
            
            void AddTrackingState(string propertyName,string partName, int toggleValue)
            {
                AnimatorState enableState = m.AddState(partName + " On");
                AnimatorState disableState = m.AddState(partName + " Off");

                InstantTransition(HoldState, enableState).AddCondition(AnimatorConditionMode.Equals, toggleValue, "LC_Tracking Control");
                InstantTransition(enableState, HoldState).AddCondition(AnimatorConditionMode.NotEqual, toggleValue, "LC_Tracking Control");

                InstantTransition(HoldState, disableState).AddCondition(AnimatorConditionMode.Equals, toggleValue+1, "LC_Tracking Control");
                InstantTransition(disableState, HoldState).AddCondition(AnimatorConditionMode.NotEqual, toggleValue+1, "LC_Tracking Control");

                void setSObject(SerializedObject o, int v)
                {
                    o.FindProperty(propertyName).enumValueIndex = v;
                    o.ApplyModifiedPropertiesWithoutUndo();
                }

                setSObject(new SerializedObject(enableState.AddStateMachineBehaviour<VRCAnimatorTrackingControl>()), 1);
                setSObject(new SerializedObject(disableState.AddStateMachineBehaviour<VRCAnimatorTrackingControl>()), 2);
            }

            AddTrackingState("trackingHead", "Head", 254);
            AddTrackingState("trackingRightHand", "Right Hand", 252);
            AddTrackingState("trackingLeftHand", "Left Hand", 250);
            AddTrackingState("trackingHip", "Hip", 248);
            AddTrackingState("trackingRightFoot", "Right Foot", 246);
            AddTrackingState("trackingLeftFoot", "Left Foot", 244);

            Debug.Log("<color=green>Added Tracking Control successfully!</color>");
        }

        private void RemoveControl()
        {
            Debug.Log("Removing Limb Control from " + myAvatar.gameObject.name);
            void RemoveControl(AnimatorController c)
            {
                if (!c)
                    return;
                for (int i=c.layers.Length-1;i>=0;i--)
                {
                    if (HasTag(c.layers[i], "Limb Control"))
                    {
                        Debug.Log("Removed Layer " + c.layers[i].name+" from "+c.name);
                        c.RemoveLayer(i);
                    }
                }
                for (int i=c.parameters.Length-1;i>=0;i--)
                {
                    if (c.parameters[i].name.StartsWith("LC_"))
                    {
                        Debug.Log("Removed Parameter " + c.parameters[i].name + " from " + c.name);
                        c.RemoveParameter(i);
                    }
                }
            }
            RemoveControl(GetPlayableLayer(myAvatar, VRCAvatarDescriptor.AnimLayerType.Base));
            RemoveControl(GetPlayableLayer(myAvatar,VRCAvatarDescriptor.AnimLayerType.Action));
            RemoveControl(GetPlayableLayer(myAvatar, VRCAvatarDescriptor.AnimLayerType.Sitting));

            if (myAvatar.expressionsMenu)
            {
                for (int i = myAvatar.expressionsMenu.controls.Count - 1; i >= 0; i--)
                {
                    if (myAvatar.expressionsMenu.controls[i].name == "Limb Control")
                    {
                        Debug.Log("Removed Limb Control Submenu from Expression Menu");
                        myAvatar.expressionsMenu.controls.RemoveAt(i);
                        EditorUtility.SetDirty(myAvatar.expressionsMenu);
                        continue;
                    }
                    if (myAvatar.expressionsMenu.controls[i].name == "Tracking Control")
                    {
                        Debug.Log("Removed Tracking Control Submenu from Expression Menu");
                        myAvatar.expressionsMenu.controls.RemoveAt(i);
                        EditorUtility.SetDirty(myAvatar.expressionsMenu);
                        continue;
                    }
                }
            }
            if (myAvatar.expressionParameters)
            {
                for (int i=myAvatar.expressionParameters.parameters.Length-1;i>=0;i--)
                {
                    if (myAvatar.expressionParameters.parameters[i].name.StartsWith("LC_"))
                    {
                        Debug.Log("Removed " + myAvatar.expressionParameters.parameters[i].name + " From Expression Parameters");
                        ArrayUtility.RemoveAt(ref myAvatar.expressionParameters.parameters, i);
                    }
                }
                EditorUtility.SetDirty(myAvatar.expressionParameters);
            }

            Debug.Log("Finished removing Limb Control!");
        }

        private void SetColorIcon(bool value)
        {
            if (value)
                GUI.backgroundColor = Color.green;
            else
                GUI.backgroundColor = Color.grey;
        }

        private void OnEnable()
        {
            if (myAvatar == null)
                myAvatar = FindObjectOfType<VRCAvatarDescriptor>();

            assetPath = PlayerPrefs.GetString("YellowToolsPath", "Assets/YellowTools/Slideshow Generator VRC/Generated Assets");
            iconTrash = new GUIContent(EditorGUIUtility.IconContent("TreeEditor.Trash")) { tooltip = "Remove Limb Control from Avatar" };
            iconError = new GUIContent(EditorGUIUtility.IconContent("console.warnicon.sml")) { tooltip = "Not enough memory available in Expression Parameters!"  };
            GetBaseTree();
        }

        private void GetBaseTree()
        {
            if (!customTree || !useCustomTree)
                customTree = Resources.Load<BlendTree>("Animations/LC_NormalBlendTree");
        }

        #region DSHelper Methods
        private static AnimatorController GetPlayableLayer(VRCAvatarDescriptor avi, VRCAvatarDescriptor.AnimLayerType type)
        {
            for (var i = 0; i < avi.baseAnimationLayers.Length; i++)
                if (avi.baseAnimationLayers[i].type == type)
                    return GetController(avi.baseAnimationLayers[i].animatorController);

            for (var i = 0; i < avi.specialAnimationLayers.Length; i++)
                if (avi.specialAnimationLayers[i].type == type)
                    return GetController(avi.specialAnimationLayers[i].animatorController);

            return null;
        }
        private static bool SetPlayableLayer(VRCAvatarDescriptor avi, VRCAvatarDescriptor.AnimLayerType type,RuntimeAnimatorController ani)
        {
            for (var i = 0; i < avi.baseAnimationLayers.Length; i++)
                if (avi.baseAnimationLayers[i].type == type)
                {
                    if (ani)
                        avi.customizeAnimationLayers = true;
                    avi.baseAnimationLayers[i].isDefault = !ani;
                    avi.baseAnimationLayers[i].animatorController = ani;
                    EditorUtility.SetDirty(avi);
                    return true;
                }

            for (var i = 0; i < avi.specialAnimationLayers.Length; i++)
                if (avi.specialAnimationLayers[i].type == type)
                {
                    if (ani)
                        avi.customizeAnimationLayers = true;
                    avi.specialAnimationLayers[i].isDefault = !ani;
                    avi.specialAnimationLayers[i].animatorController = ani;
                    EditorUtility.SetDirty(avi);
                    return true;
                }


            return false;
        }
        private static AnimatorController GetController(RuntimeAnimatorController controller)
        {
            return AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GetAssetPath(controller));
        }

        private static AnimatorStateTransition InstantTransition(AnimatorState state, AnimatorState destination)
        {
            var t = state.AddTransition(destination, false);
            t.duration = 0;
            return t;
        }
        private static void AddTag(AnimatorControllerLayer layer, string tag)
        {
            if (HasTag(layer, tag)) return;

            var t = layer.stateMachine.AddAnyStateTransition((AnimatorState)null);
            t.isExit = true;
            t.mute = true;
            t.name = tag;
        }
        private static bool HasTag(AnimatorControllerLayer layer, string tag)
        {
            return layer.stateMachine.anyStateTransitions.Any(t => t.isExit && t.mute && t.name == tag);
        }

        private static AnimatorControllerLayer AddLayer(AnimatorController controller, string name, float defaultWeight, AvatarMask mask = null)
        {
            var newLayer = new AnimatorControllerLayer
            {
                name = name,
                defaultWeight = defaultWeight,
                avatarMask = mask,
                stateMachine = new AnimatorStateMachine
                {
                    name = name,
                    hideFlags = HideFlags.HideInHierarchy
                },
            };
            AssetDatabase.AddObjectToAsset(newLayer.stateMachine, controller);
            controller.AddLayer(newLayer);
            return newLayer;
        }
        private static void ReadyParameter(AnimatorController controller, string parameter, AnimatorControllerParameterType type)
        {
            if (!GetParameter(controller, parameter, out _))
                controller.AddParameter(parameter, type);
        }
        private static bool GetParameter(AnimatorController controller, string parameter, out int index)
        {
            index = -1;
            for (int i = 0; i < controller.parameters.Length; i++)
            {
                if (controller.parameters[i].name == parameter)
                {
                    index = i;
                    return true;
                }
            }
            return false;
        }

        private static void AssetFolderPath(ref string variable,string title, string playerpref)
        {
            using (new GUILayout.HorizontalScope())
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField(title, variable);
                EditorGUI.EndDisabledGroup();
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    var dummyPath = EditorUtility.OpenFolderPanel(title, AssetDatabase.IsValidFolder(variable) ? variable : string.Empty, string.Empty);
                    if (string.IsNullOrEmpty(dummyPath))
                        return;

                    if (!dummyPath.StartsWith("Assets"))
                    {
                        Debug.LogWarning("New Path must be a folder within Assets!");
                        return;
                    }

                    variable = FileUtil.GetProjectRelativePath(dummyPath);
                    PlayerPrefs.SetString(playerpref, variable);
                }
            }
        }
        private static void Credit()
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Made By Yellow#0720","boldlabel"))
                    Application.OpenURL("err");
            }
        }

        private static void ReadyPath(string folderPath)
        {
            string[] folderNames = folderPath.Split('/');
            string[] folderPaths = new string[folderNames.Length];

            for (int i = 0; i < folderNames.Length; i++)
            {
                folderPaths[i] = folderNames[0];
                for (int j = 1; j <= i; j++)
                    folderPaths[i] += $"/{folderNames[j]}";
            }

            for (int i = 1; i < folderPaths.Length; i++)
                if (!AssetDatabase.IsValidFolder(folderPaths[i]))
                    AssetDatabase.CreateFolder(folderPaths[i-1],folderNames[i]);
        }

        private static T CopyAssetAndReturn<T>(string path, string newpath) where T : Object
        {
            if (path != newpath)
                AssetDatabase.CopyAsset(path, newpath);
            return AssetDatabase.LoadAssetAtPath<T>(newpath);

        }
        
        private static T CopyAssetAndReturn<T>(Object obj, string newPath) where T : Object
        {
            string assetPath = AssetDatabase.GetAssetPath(obj);
            Object myAsset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (myAsset && myAsset != obj)
            {
                Object[] subObjects = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                for (int i = 0; i < subObjects.Length; i++)
                {
                    if (subObjects[i] == obj)
                    {
                        Object newAsset = Object.Instantiate(subObjects[i]);
                        AssetDatabase.CreateAsset(newAsset, newPath);
                        return AssetDatabase.LoadAssetAtPath<T>(newPath);
                    }

                }
                return null;
            }
            else
            {
                if (myAsset)
                {
                    AssetDatabase.CopyAsset(assetPath, newPath);
                    return AssetDatabase.LoadAssetAtPath<T>(newPath);
                }
                else
                    return null;
            }
        }

        private static VRCExpressionParameters ReplaceParameters(VRCAvatarDescriptor avi, string folderPath)
        {
            avi.customExpressions = true;
            if (avi.expressionParameters)
            {
                avi.expressionParameters = CopyAssetAndReturn<VRCExpressionParameters>(avi.expressionParameters, AssetDatabase.GenerateUniqueAssetPath(folderPath + "/" + avi.expressionParameters.name + ".asset"));
                return avi.expressionParameters;
            }

            var assetPath = folderPath + "/" + avi.gameObject.name + " Parameters.asset";
            var newParameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            AssetDatabase.CreateAsset(newParameters, AssetDatabase.GenerateUniqueAssetPath(assetPath));
            AssetDatabase.ImportAsset(assetPath);
            avi.expressionParameters = newParameters;
            avi.customExpressions = true;
            return newParameters;
        }

        private static VRCExpressionsMenu ReplaceMenu(VRCExpressionsMenu menu, string folderPath, bool deep = true, Dictionary<VRCExpressionsMenu, VRCExpressionsMenu> copyDict = null)
        {
            VRCExpressionsMenu newMenu;
            if (!menu)
                return null;
            if (copyDict == null)
                copyDict = new Dictionary<VRCExpressionsMenu, VRCExpressionsMenu>();
            
            if (copyDict.ContainsKey(menu))
                newMenu = copyDict[menu];
            else
            {
                newMenu = CopyAssetAndReturn<VRCExpressionsMenu>(menu, AssetDatabase.GenerateUniqueAssetPath(folderPath + "/" + menu.name + ".asset"));
                copyDict.Add(menu, newMenu);
                if (!deep) return newMenu;

                foreach (var c in newMenu.controls.Where(c => c.type == VRCExpressionsMenu.Control.ControlType.SubMenu && c.subMenu != null))
                {
                    c.subMenu = ReplaceMenu(c.subMenu, folderPath, true, copyDict);
                }

                EditorUtility.SetDirty(newMenu);
            }
            return newMenu;
        }

        private static VRCExpressionsMenu ReplaceMenu(VRCAvatarDescriptor avi, string folderPath, bool deep = false)
        {
            avi.customExpressions = true;
            if (avi.expressionsMenu)
            {
                avi.expressionsMenu = ReplaceMenu(avi.expressionsMenu, folderPath, deep);
                return avi.expressionsMenu;
            }


            var newMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            AssetDatabase.CreateAsset(newMenu, AssetDatabase.GenerateUniqueAssetPath(folderPath + "/" + avi.gameObject.name + " Menu.asset"));
            avi.expressionsMenu = newMenu;
            avi.customExpressions = true;
            return newMenu;

        }
        
        private static void AddControls(VRCExpressionsMenu target, List<VRCExpressionsMenu.Control> newCons)
        {
            foreach (var c in newCons)
                target.controls.Add(c);
            
            EditorUtility.SetDirty(target);
        }

        private static void AddParameters(VRCExpressionParameters target, List<VRCExpressionParameters.Parameter> newParams)
        {
            target.parameters = target.parameters == null || target.parameters.Length <= 0 ? newParams.ToArray() : target.parameters.Concat(newParams).ToArray();
            
            EditorUtility.SetDirty(target);
            AssetDatabase.WriteImportSettingsIfDirty(AssetDatabase.GetAssetPath(target));
        }
        private static string GenerateUniqueString(string s, System.Func<string, bool> check)
        {
            if (check(s))
                return s;

            int suffix = 0;

            int.TryParse(s.Substring(s.Length - 2, 2), out int d);
            if (d >= 0)
                suffix = d;
            if (suffix > 0) s = suffix > 9 ? s.Substring(0, s.Length - 2) : s.Substring(0, s.Length - 1);

            s = s.Trim();

            suffix++;

            string newString = s + " " + suffix;
            while (!check(newString))
            {
                suffix++;
                newString = s + " " + suffix;
            }

            return newString;
        }
        #endregion
    }
}
#endif
