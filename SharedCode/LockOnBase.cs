﻿using System;
using System.Collections.Generic;
using System.Reflection;
using IllusionPlugin;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LockOnPlugin
{
    internal abstract class LockOnBase : MonoBehaviour
    {
        public const string VERSION = "2.4.0";
        public const string NAME_HSCENEMAKER = "LockOnPlugin";
        public const string NAME_NEO = "LockOnPluginNeo";

        public static bool lockedOn = false;

        protected abstract float CameraMoveSpeed { get; set; }
        protected abstract Vector3 CameraTargetPos { get; set; }
        protected abstract Vector3 CameraAngle { get; set; }
        protected abstract float CameraFov { get; set; }
        protected abstract Vector3 CameraDir { get; set; }
        protected abstract bool CameraTargetTex { set; }
        protected abstract float CameraZoomSpeed { get; }
        protected abstract Transform CameraTransform { get; }
        protected virtual Vector3 CameraForward => CameraTransform.forward;
        protected virtual Vector3 CameraRight => CameraTransform.right;
        protected virtual Vector3 CameraUp => CameraTransform.up;
        protected virtual Vector3 LockOnTargetPos => lockOnTarget.transform.position;
        protected virtual bool AllowTracking => true;
        protected virtual bool InputFieldSelected => false;

        protected Hotkey lockOnHotkey;
        protected Hotkey lockOnGuiHotkey;
        protected Hotkey prevCharaHotkey;
        protected Hotkey nextCharaHotkey;
        protected Hotkey rotationHotkey;
        protected float lockedMinDistance;
        protected float trackingSpeedNormal;
        protected float trackingSpeedRotation = 0.2f;
        protected bool manageCursorVisibility;
        protected bool scrollThroughMalesToo;
        protected bool showInfoMsg;

        protected bool controllerEnabled;
        protected float controllerMoveSpeed;
        protected float controllerZoomSpeed;
        protected float controllerRotSpeed;
        protected bool controllerInvertX;
        protected bool controllerInvertY;
        protected bool swapSticks;

        protected CameraTargetManager targetManager;
        protected CharInfo currentCharaInfo;
        protected GameObject lockOnTarget;
        protected Vector3? lastTargetPos;
        protected Vector3 lastTargetAngle;
        protected bool lockRotation = false;
        protected bool showLockOnTargets = false;
        protected float defaultCameraSpeed;
        protected float targetSize = 25f;
        protected float guiTimeAngle = 0f;
        protected float guiTimeFov = 0f;
        protected float guiTimeInfo = 0f;
        protected string infoMsg = "";
        protected Vector2 infoMsgPosition = new Vector2(0.5f, 0f);
        protected Vector3 targetOffsetSize = new Vector3();
        protected Vector3 targetOffsetSizeAdded = new Vector3();
        protected float dpadXTimeHeld = 0f;
        protected float offsetKeyHeld = 0f;
        protected bool reduceOffset = false;
        protected bool mouseButtonDown0 = false;
        protected bool mouseButtonDown1 = false;

        protected virtual void Start()
        {
            targetManager = new CameraTargetManager();
            defaultCameraSpeed = CameraMoveSpeed;
            LoadSettings();
        }

        protected virtual void LoadSettings()
        {
            lockOnHotkey = new Hotkey(ModPrefs.GetString("LockOnPlugin.Hotkeys", "LockOnHotkey", "N", true), 0.4f);
            lockOnGuiHotkey = new Hotkey(ModPrefs.GetString("LockOnPlugin.Hotkeys", "LockOnGuiHotkey", "K", true));
            prevCharaHotkey = new Hotkey(ModPrefs.GetString("LockOnPlugin.Hotkeys", "PrevCharaHotkey", "False", true));
            nextCharaHotkey = new Hotkey(ModPrefs.GetString("LockOnPlugin.Hotkeys", "NextCharaHotkey", "L", true));
            rotationHotkey = new Hotkey(ModPrefs.GetString("LockOnPlugin.Hotkeys", "RotationHotkey", "False", true));

            lockedMinDistance = Mathf.Abs(ModPrefs.GetFloat("LockOnPlugin.Misc", "LockedMinDistance", 0f, true));
            trackingSpeedNormal = Mathf.Clamp(ModPrefs.GetFloat("LockOnPlugin.Misc", "LockedTrackingSpeed", 0.1f, true), 0.01f, 1f);
            showInfoMsg = ModPrefs.GetString("LockOnPlugin.Misc", "ShowInfoMsg", "False", true).ToLower() == "true" ? true : false;
            manageCursorVisibility = ModPrefs.GetString("LockOnPlugin.Misc", "ManageCursorVisibility", "True", true).ToLower() == "true" ? true : false;
            CameraTargetTex = ModPrefs.GetString("LockOnPlugin.Misc", "HideCameraTarget", "True", true).ToLower() == "true" ? false : true;
            scrollThroughMalesToo = ModPrefs.GetString("LockOnPlugin.Misc", "ScrollThroughMalesToo", "False", true).ToLower() == "true" ? true : false;

            controllerEnabled = ModPrefs.GetString("LockOnPlugin.Gamepad", "ControllerEnabled", "True", true).ToLower() == "true" ? true : false;
            controllerMoveSpeed = ModPrefs.GetFloat("LockOnPlugin.Gamepad", "ControllerMoveSpeed", 0.3f, true);
            controllerZoomSpeed = ModPrefs.GetFloat("LockOnPlugin.Gamepad", "ControllerZoomSpeed", 0.2f, true);
            controllerRotSpeed = ModPrefs.GetFloat("LockOnPlugin.Gamepad", "ControllerRotSpeed", 0.4f, true);
            controllerInvertX = ModPrefs.GetString("LockOnPlugin.Gamepad", "ControllerInvertX", "False", true).ToLower() == "true" ? true : false;
            controllerInvertY = ModPrefs.GetString("LockOnPlugin.Gamepad", "ControllerInvertY", "False", true).ToLower() == "true" ? true : false;
            swapSticks = ModPrefs.GetString("LockOnPlugin.Gamepad", "SwapSticks", "False", true).ToLower() == "true" ? true : false;
        }

        protected virtual void Update()
        {
            Hotkey.inputFieldSelected = InputFieldSelected;
            targetManager.UpdateCustomTargetTransforms();
            GamepadControls();

            lockOnHotkey.KeyHoldAction(LockOnRelease);
            lockOnHotkey.KeyUpAction(() => LockOn());
            lockOnGuiHotkey.KeyDownAction(ToggleLockOnGUI);
            prevCharaHotkey.KeyDownAction(() => CharaSwitch(false));
            nextCharaHotkey.KeyDownAction(() => CharaSwitch(true));
            //rotationHotkey.KeyDownAction(RotationLockToggle);

            if(lockOnTarget)
            {
                if(Input.GetMouseButton(0) && Input.GetMouseButton(1))
                {
                    float x = Input.GetAxis("Mouse X");
                    float y = Input.GetAxis("Mouse Y");
                    if(Mathf.Abs(x) > 0f || Mathf.Abs(y) > 0f)
                    {
                        targetOffsetSize += (CameraRight * x * defaultCameraSpeed) + (CameraForward * y * defaultCameraSpeed);
                        reduceOffset = false;
                    }
                }
                else if(Input.GetMouseButton(1))
                {
                    float x = Input.GetAxis("Mouse X");
                    if(Input.GetKey("left ctrl"))
                    {
                        guiTimeAngle = 0.1f;
                        if(Mathf.Abs(x) > 0f)
                        {
                            //camera tilt adjustment
                            float newAngle = CameraAngle.z - x;
                            newAngle = Mathf.Repeat(newAngle, 360f);
                            CameraAngle = new Vector3(CameraAngle.x, CameraAngle.y, newAngle);
                        }
                    }
                    else if(Input.GetKey("left shift"))
                    {
                        guiTimeFov = 0.1f;
                        if(Mathf.Abs(x) > 0f)
                        {
                            //fov adjustment
                            float newFov = CameraFov + x;
                            CameraFov = Mathf.Clamp(newFov, 1f, 160f);
                        }
                    }
                    else if(!InputFieldSelected)
                    {
                        if(Mathf.Abs(x) > 0f)
                        {
                            //handle zooming manually when camera.movespeed == 0
                            float newDir = CameraDir.z - x * CameraZoomSpeed;
                            newDir = Mathf.Clamp(newDir, -float.MaxValue, 0f);
                            CameraDir = new Vector3(0f, 0f, newDir);
                            reduceOffset = false;
                        }

                        float y = Input.GetAxis("Mouse Y");
                        if(Mathf.Abs(y) > 0f)
                        {
                            targetOffsetSize += (Vector3.up * y * defaultCameraSpeed);
                            reduceOffset = false;
                        }
                    }
                }
                
                float speed = Time.deltaTime * Mathf.Lerp(0.2f, 2f, offsetKeyHeld);
                bool RightArrow = Input.GetKey(KeyCode.RightArrow), LeftArrow = Input.GetKey(KeyCode.LeftArrow);
                bool UpArrow = Input.GetKey(KeyCode.UpArrow), DownArrow = Input.GetKey(KeyCode.DownArrow);
                bool PageUp = Input.GetKey(KeyCode.PageUp), PageDown = Input.GetKey(KeyCode.PageDown);

                if(!InputFieldSelected && Hotkey.allowHotkeys && (RightArrow || LeftArrow || UpArrow || DownArrow || PageUp || PageDown))
                {
                    reduceOffset = false;

                    offsetKeyHeld += Time.deltaTime / 3f;
                    if(offsetKeyHeld > 1f) offsetKeyHeld = 1f;

                    if(RightArrow)
                    {
                        targetOffsetSize += CameraRight * speed;
                    }
                    else if(LeftArrow)
                    {
                        targetOffsetSize += CameraRight * -speed;
                    }
                    if(UpArrow)
                    {
                        targetOffsetSize += CameraForward * speed;
                    }
                    else if(DownArrow)
                    {
                        targetOffsetSize += CameraForward * -speed;
                    }
                    if(PageUp)
                    {
                        targetOffsetSize += CameraUp * speed;
                    }
                    else if(PageDown)
                    {
                        targetOffsetSize += CameraUp * -speed;
                    }
                }
                else
                {
                    offsetKeyHeld -= Time.deltaTime * 2f;
                    if(offsetKeyHeld < 0f) offsetKeyHeld = 0f;
                }

                if(reduceOffset)
                {
                    if(targetOffsetSize.magnitude > 0.00001f)
                    {
                        targetOffsetSize = Vector3.MoveTowards(targetOffsetSize, new Vector3(), targetOffsetSize.magnitude / 10); 
                    }
                    else
                    {
                        targetOffsetSize = new Vector3();
                        reduceOffset = false;
                    }
                }

                if(AllowTracking)
                {
                    float trackingSpeed = (lockRotation && trackingSpeedNormal < trackingSpeedRotation) ? trackingSpeedRotation : trackingSpeedNormal;
                    float distance = Vector3.Distance(CameraTargetPos, lastTargetPos.Value);
                    if(distance > 0.00001f) CameraTargetPos = Vector3.MoveTowards(CameraTargetPos, LockOnTargetPos + targetOffsetSize, distance * trackingSpeed * Time.deltaTime * 60);
                    CameraTargetPos += targetOffsetSize - targetOffsetSizeAdded;
                    targetOffsetSizeAdded = targetOffsetSize;
                    lastTargetPos = LockOnTargetPos + targetOffsetSize; 
                }
            }

            if(lockRotation)
            {
                Vector3 targetAngle = CameraAdjustedEulerAngles(lockOnTarget, CameraTransform);
                Vector3 difference = targetAngle - lastTargetAngle;
                CameraAngle += new Vector3(-difference.x, -difference.y, -difference.z);
                lastTargetAngle = targetAngle;
            }

            if(Input.GetKeyDown(KeyCode.Escape))
            {
                showLockOnTargets = false;
            }

            if(GUIUtility.hotControl == 0 && !EventSystem.current.IsPointerOverGameObject() && Hotkey.allowHotkeys && manageCursorVisibility)
            {
                if(Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
                {
                    if(Input.GetMouseButtonDown(0)) mouseButtonDown0 = true;
                    if(Input.GetMouseButtonDown(1)) mouseButtonDown1 = true;

                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                }
            }

            if((mouseButtonDown0 || mouseButtonDown1) && (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1)))
            {
                if(Input.GetMouseButtonUp(0)) mouseButtonDown0 = false;
                if(Input.GetMouseButtonUp(1)) mouseButtonDown1 = false;

                if(!mouseButtonDown1 && !mouseButtonDown0)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true; 
                }
            }
        }
        
        protected virtual void OnGUI()
        {
            if(showInfoMsg && guiTimeInfo > 0f)
            {
                DebugGUI(infoMsgPosition.x, infoMsgPosition.y, 200f, 45f, infoMsg);
                guiTimeInfo -= Time.deltaTime;
            }

            if(guiTimeAngle > 0f)
            {
                DebugGUI(0.5f, 0.5f, 100f, 50f, "Camera tilt\n" + CameraAngle.z.ToString("0.0"));
                guiTimeAngle -= Time.deltaTime;
            }

            if(guiTimeFov > 0f)
            {
                DebugGUI(0.5f, 0.5f, 100f, 50f, "Field of view\n" + CameraFov.ToString("0.0"));
                guiTimeFov -= Time.deltaTime;
            }

            if(showLockOnTargets)
            {
                List<GameObject> targets = targetManager.GetAllTargets();
                for(int i = 0; i < targets.Count; i++)
                {
                    Vector3 pos = Camera.main.WorldToScreenPoint(targets[i].transform.position);
                    if(pos.z > 0f && GUI.Button(new Rect(pos.x - targetSize / 2f, Screen.height - pos.y - targetSize / 2f, targetSize, targetSize), "L"))
                    {
                        CameraTargetPos += targetOffsetSize;
                        targetOffsetSize = new Vector3();
                        LockOn(targets[i]);
                    }
                }
            }
        }

        protected virtual bool LockOn()
        {
            if(currentCharaInfo)
            {
                if(reduceOffset == true)
                {
                    CameraTargetPos += targetOffsetSize;
                    targetOffsetSize = new Vector3();
                }
                else if(targetOffsetSize.magnitude > 0f)
                {
                    reduceOffset = true;
                    return true;
                }

                List<string> targetList = currentCharaInfo is CharFemale ? FileManager.GetQuickFemaleTargetNames() : FileManager.GetQuickMaleTargetNames();
                
                if(!lockOnTarget)
                {
                    return LockOn(targetList[0]);
                }
                else
                {
                    for(int i = 0; i < targetList.Count; i++)
                    {
                        if(lockOnTarget.name == targetList[i])
                        {
                            int next = i + 1 > targetList.Count - 1 ? 0 : i + 1;
                            return LockOn(targetList[next]);
                        }
                    }
                    
                    return LockOn(targetList[0]);
                }
            }

            return false;
        }

        protected virtual bool LockOn(string targetName, bool lockOnAnyway = false, bool resetOffset = true)
        {
            foreach(GameObject target in targetManager.GetAllTargets())
            {
                if(target.name.Substring(3) == targetName.Substring(3))
                {
                    if(LockOn(target, resetOffset))
                    {
                        return true;
                    }
                }
            }

            if(lockOnAnyway)
            {
                if(LockOn())
                {
                    return true;
                }
            }

            return false;
        }

        protected virtual bool LockOn(GameObject target, bool resetOffset = true)
        {
            if(target)
            {
                if(resetOffset) reduceOffset = true;
                lockedOn = true;
                lockOnTarget = target;
                if(lastTargetPos == null) lastTargetPos = LockOnTargetPos + targetOffsetSize;
                CameraMoveSpeed = 0f;
                CreateInfoMsg("Locked to \"" + lockOnTarget.name + "\"");
                return true;
            }

            return false;
        }

        protected virtual void LockOnRelease()
        {
            if(lockOnTarget)
            {
                lockedOn = false;
                reduceOffset = true;
                lockOnTarget = null;
                lastTargetPos = null;
                lockRotation = false;
                CameraMoveSpeed = defaultCameraSpeed;
                CreateInfoMsg("Camera unlocked");
            }
        }

        protected virtual void ToggleLockOnGUI()
        {
            if(currentCharaInfo)
            {
                showLockOnTargets = !showLockOnTargets;
            }
        }

        protected virtual void CharaSwitch(bool scrollDown = true)
        {
            Console.WriteLine("Character switching not implemented in this version");
        }

        protected virtual void RotationLockToggle()
        {
            if(lockRotation)
            {
                lockRotation = false;
                CreateInfoMsg("Rotation released");
            }
            else
            {
                lockRotation = true;
                lastTargetAngle = CameraAdjustedEulerAngles(lockOnTarget, CameraTransform);
                CreateInfoMsg("Rotation locked");
            }
        }

        protected void CreateInfoMsg(string msg, float time = 3f)
        {
            infoMsg = msg;
            guiTimeInfo = time;
        }

        protected static bool DebugGUI(float screenWidthMult, float screenHeightMult, float width, float height, string msg)
        {
            float xpos = Screen.width * screenWidthMult - width / 2f;
            float ypos = Screen.height * screenHeightMult - height / 2f;
            xpos = Mathf.Clamp(xpos, 0f, Screen.width - width);
            ypos = Mathf.Clamp(ypos, 0f, Screen.height - height);
            return GUI.Button(new Rect(xpos, ypos, width, height), msg);
        }

        protected static FieldType GetSecureField<FieldType, ObjectType>(string fieldName, ObjectType target = null)
            where FieldType : class
            where ObjectType : UnityEngine.Object
        {
            if(target.Equals(null))
            {
                target = FindObjectOfType<ObjectType>();
            }

            if(!target.Equals(null))
            {
                FieldInfo fieldinfo = typeof(ObjectType).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
                if(!fieldinfo.Equals(null))
                {
                    return fieldinfo.GetValue(target) as FieldType;
                }
            }

            return null;
        }

        protected static object InvokePluginMethod(string typeName, string methodName, object[] parameters = null)
        {
            Type type = FindType(typeName);

            if(type != null)
            {
                UnityEngine.Object instance = FindObjectOfType(type);

                if(instance != null)
                {
                    MethodInfo methodInfo = type.GetMethod(methodName);

                    if(methodInfo != null)
                    {
                        if(methodInfo.GetParameters().Length == 0)
                        {
                            return methodInfo.Invoke(instance, null);
                        }
                        else
                        {
                            return methodInfo.Invoke(instance, parameters);
                        }
                    } 
                }
            }

            return null;
        }

        protected static Type FindType(string qualifiedTypeName)
        {
            Type t = Type.GetType(qualifiedTypeName);

            if(t != null)
            {
                return t;
            }
            else
            {
                foreach(Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    t = asm.GetType(qualifiedTypeName);
                    if(t != null)
                    {
                        return t;
                    }
                }

                return null;
            }
        }

        protected static Vector3 CameraAdjustedEulerAngles(GameObject target, Transform cameraTransform)
        {
            float x = AngleSigned(target.transform.forward, Vector3.forward, cameraTransform.right);
            float y = AngleSigned(target.transform.right, Vector3.right, cameraTransform.up);
            float z = AngleSigned(target.transform.up, Vector3.up, cameraTransform.forward);
            return new Vector3(x, y, z);
        }

        protected static float AngleSigned(Vector3 v1, Vector3 v2, Vector3 n)
        {
            return Mathf.Atan2(
                Vector3.Dot(n, Vector3.Cross(v1, v2)),
                Vector3.Dot(v1, v2)) * Mathf.Rad2Deg;
        }

        protected virtual void GamepadControls()
        {
            if(!controllerEnabled) return;
            if(Input.GetJoystickNames().Length == 0) return;
            
            if(Input.GetKeyDown(KeyCode.JoystickButton0))
            {
                LockOn();
            }

            if(Input.GetKeyDown(KeyCode.JoystickButton1))
            {
                LockOnRelease();
            }

            if(Input.GetKeyDown(KeyCode.JoystickButton3))
            {
                CharaSwitch(true);
            }

            Vector2 leftStick = new Vector2(Input.GetAxis("Oculus_GearVR_LThumbstickX"), -Input.GetAxis("Oculus_GearVR_LThumbstickY"));
            Vector2 rightStick = new Vector2(-Input.GetAxis("Oculus_GearVR_RThumbstickY"), Input.GetAxis("Oculus_GearVR_DpadX"));
            KeyCode L1 = KeyCode.JoystickButton4;
            KeyCode R1 = KeyCode.JoystickButton5;

            if(swapSticks)
            {
                Vector2 temp = rightStick;
                rightStick = leftStick;
                leftStick = temp;
                L1 = KeyCode.JoystickButton5;
                R1 = KeyCode.JoystickButton4;
            }

            if(leftStick.magnitude > 0.2f)
            {
                if(Input.GetKey(R1))
                {
                    guiTimeFov = 1f;
                    float newFov = CameraFov + leftStick.y * Time.deltaTime * 60f;
                    CameraFov = Mathf.Clamp(newFov, 1f, 160f);
                }
                else if(Input.GetKey(L1))
                {
                    float newDir = CameraDir.z - leftStick.y * Mathf.Lerp(0.01f, 0.4f, controllerZoomSpeed) * Time.deltaTime * 60f;
                    newDir = Mathf.Clamp(newDir, float.MinValue, 0f);
                    CameraDir = new Vector3(0f, 0f, newDir);
                }
                else
                {
                    float speed = Mathf.Lerp(1f, 4f, controllerRotSpeed) * Time.deltaTime * 60f;
                    float newX = Mathf.Repeat((controllerInvertX || CameraDir.z == 0f ? leftStick.y : -leftStick.y) * speed, 360f);
                    float newY = Mathf.Repeat((controllerInvertY || CameraDir.z == 0f ? leftStick.x : -leftStick.x) * speed, 360f);
                    CameraAngle += new Vector3(newX, newY, 0f);
                }
            }

            if(rightStick.magnitude > 0.2f)
            {
                reduceOffset = false;
                float speed = (Input.GetKey(R1) ? Mathf.Lerp(0.01f, 0.4f, controllerZoomSpeed) : Mathf.Lerp(0.001f, 0.04f, controllerMoveSpeed)) * Time.deltaTime * 60f;
                if(lockOnTarget)
                {
                    if(Input.GetKey(R1))
                    {
                        targetOffsetSize += (CameraForward * -rightStick.y * speed);
                    }
                    else
                    {
                        targetOffsetSize += (CameraRight * rightStick.x * speed) + (Vector3.up * -rightStick.y * speed);
                    }
                }
                else
                {
                    if(Input.GetKey(R1))
                    {
                        CameraTargetPos += (CameraForward * -rightStick.y * speed);
                    }
                    else
                    {
                        CameraTargetPos += (CameraRight * rightStick.x * speed) + (Vector3.up * -rightStick.y * speed);
                    }
                }
            }

            float dpadX = -Input.GetAxis("Oculus_GearVR_DpadY");
            if(Math.Abs(dpadX) > 0f)
            {
                if(dpadXTimeHeld == 0f || dpadXTimeHeld > 0.15f)
                {
                    guiTimeAngle = 1f;
                    float newAngle = CameraAngle.z - dpadX * Time.deltaTime * 60f;
                    newAngle = Mathf.Repeat(newAngle, 360f);
                    CameraAngle = new Vector3(CameraAngle.x, CameraAngle.y, newAngle);
                }

                dpadXTimeHeld += Time.deltaTime;
            }
            else
            {
                dpadXTimeHeld = 0f;
            }
        }
    }
}
