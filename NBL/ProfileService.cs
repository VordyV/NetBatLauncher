using System;
using System.IO;
using System.Threading.Tasks;

namespace NBL.Services;

public sealed class ProfileService
{
    private readonly string ProfilesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Battlefield 2142", "Profiles");

    private readonly string StoragePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NetBat2142", "Profiles");

    private const string DefaultGlobal = """
GlobalSettings.setDefaultUser ""
GlobalSettings.setLastOnlineUser ""
GlobalSettings.setEncryptedLogin ""
GlobalSettings.setNamePrefix ""
""";

    private const string DefaultProfile = """
LocalProfile.setName "Default"
LocalProfile.setGamespyNick ""
LocalProfile.setEAOnlineMasterAccount ""
LocalProfile.setEAOnlineSubAccount ""
LocalProfile.setTotalPlayedTime 0
LocalProfile.setNumTimesLoggedIn 0
LocalProfile.setRank 0
LocalProfile.setCareerPoints 0
LocalProfile.setLastBaseUpdate 0
LocalProfile.setCurrentProfileHeavyArmor 0 0 0
LocalProfile.setCurrentProfileKitSetting 0 0 0 0
LocalProfile.setCurrentProfileKitSetting 0 0 1 0
LocalProfile.setCurrentProfileKitSetting 0 0 2 0
LocalProfile.setCurrentProfileKitSetting 0 0 3 0
LocalProfile.setCurrentProfileHeavyArmor 0 1 0
LocalProfile.setCurrentProfileKitSetting 0 1 0 0
LocalProfile.setCurrentProfileKitSetting 0 1 1 0
LocalProfile.setCurrentProfileKitSetting 0 1 2 0
LocalProfile.setCurrentProfileKitSetting 0 1 3 0
LocalProfile.setCurrentProfileHeavyArmor 0 2 0
LocalProfile.setCurrentProfileKitSetting 0 2 0 0
LocalProfile.setCurrentProfileKitSetting 0 2 1 0
LocalProfile.setCurrentProfileKitSetting 0 2 2 0
LocalProfile.setCurrentProfileKitSetting 0 2 3 0
LocalProfile.setCurrentProfileHeavyArmor 0 3 0
LocalProfile.setCurrentProfileKitSetting 0 3 0 0
LocalProfile.setCurrentProfileKitSetting 0 3 1 0
LocalProfile.setCurrentProfileKitSetting 0 3 2 0
LocalProfile.setCurrentProfileKitSetting 0 3 3 0
LocalProfile.setCurrentProfileHeavyArmor 1 0 0
LocalProfile.setCurrentProfileKitSetting 1 0 0 0
LocalProfile.setCurrentProfileKitSetting 1 0 1 0
LocalProfile.setCurrentProfileKitSetting 1 0 2 0
LocalProfile.setCurrentProfileKitSetting 1 0 3 0
LocalProfile.setCurrentProfileHeavyArmor 1 1 0
LocalProfile.setCurrentProfileKitSetting 1 1 0 0
LocalProfile.setCurrentProfileKitSetting 1 1 1 0
LocalProfile.setCurrentProfileKitSetting 1 1 2 0
LocalProfile.setCurrentProfileKitSetting 1 1 3 0
LocalProfile.setCurrentProfileHeavyArmor 1 2 0
LocalProfile.setCurrentProfileKitSetting 1 2 0 0
LocalProfile.setCurrentProfileKitSetting 1 2 1 0
LocalProfile.setCurrentProfileKitSetting 1 2 2 0
LocalProfile.setCurrentProfileKitSetting 1 2 3 0
LocalProfile.setCurrentProfileHeavyArmor 1 3 0
LocalProfile.setCurrentProfileKitSetting 1 3 0 0
LocalProfile.setCurrentProfileKitSetting 1 3 1 0
LocalProfile.setCurrentProfileKitSetting 1 3 2 0
LocalProfile.setCurrentProfileKitSetting 1 3 3 0
""";

    private const string DefaultVideo = """
VideoSettings.setTerrainQuality 3
VideoSettings.setGeometryQuality 3
VideoSettings.setLightingQuality 3
VideoSettings.setDynamicLightingQuality 3
VideoSettings.setDynamicShadowsQuality 3
VideoSettings.setEffectsQuality 3
VideoSettings.setTextureQuality 3
VideoSettings.setTextureFilteringQuality 3
VideoSettings.setResolution 1920x1080@60Hz
VideoSettings.setAntialiasing 4Samples
VideoSettings.setViewDistanceScale 1
VideoSettings.setUseBloom 1
VideoSettings.setVideoOptionScheme 3
""";

    private const string DefaultAudio = """
AudioSettings.setVoipEnabled 1
AudioSettings.setVoipPlaybackVolume 1
AudioSettings.setVoipCaptureVolume 1
AudioSettings.setVoipCaptureThreshold 1
AudioSettings.setVoipBoostEnabled 1
AudioSettings.setVoipUsePushToTalk 1
AudioSettings.setProvider "software"
AudioSettings.setSoundQuality "High"
AudioSettings.setEffectsVolume 1
AudioSettings.setMusicVolume 1
AudioSettings.setHelpVoiceVolume 0
AudioSettings.setEnglishOnlyVoices 0
AudioSettings.setEnableEAX 1
""";

    private const string DefaultServerSettings = """
GameServerSettings.setServerName "Battlefield 2142"
GameServerSettings.setPassword ""
GameServerSettings.setInternet 0
GameServerSettings.setMaxPlayers 64
GameServerSettings.setSpawnTime 15
GameServerSettings.setTicketRatio 100
GameServerSettings.setRoundsPerMap 3
GameServerSettings.setTimeLimit 0
GameServerSettings.setScoreLimit 0
GameServerSettings.setSoldierFF 100
GameServerSettings.setVehicleFF 100
GameServerSettings.setSoldierSplashFF 100
GameServerSettings.setVehicleSplashFF 100
GameServerSettings.setPunishTeamKills 1
GameServerSettings.setVotingEnabled 1
GameServerSettings.setVoteTime 90
GameServerSettings.setMinPlayersForVoting 2
GameServerSettings.setVoipEnabled 1
GameServerSettings.setVoipQuality 5
GameServerSettings.setVoipServerRemote 0
GameServerSettings.setVoipServerRemoteIP 
GameServerSettings.setVoipServerPort 55125
GameServerSettings.setVoipBFClientPort 55123
GameServerSettings.setVoipBFServerPort 55124
GameServerSettings.setVoipSharedPassword 
GameServerSettings.setAutoRecord 0
GameServerSettings.setSvPunkBuster 0
GameServerSettings.setTeamRatio 100
GameServerSettings.setAutoBalanceTeam 0
GameServerSettings.setGameMode 
""";

    private const string DefaultControls = """
ControlMap.create InfantryPlayerInputControlMap
ControlMap.addKeysToAxisMapping c_PIYaw IDFKeyboard IDKey_D IDKey_A 0
ControlMap.addKeysToAxisMapping c_PIThrottle IDFKeyboard IDKey_W IDKey_S 0
ControlMap.addButtonToTriggerMapping c_PIFire IDFMouse IDButton_0 0 0
ControlMap.addKeyToTriggerMapping c_PIAction IDFKeyboard IDKey_Space 10000 0
ControlMap.addKeyToTriggerMapping c_PIAltSprint IDFKeyboard IDKey_LeftShift 0 0
ControlMap.addKeyToTriggerMapping c_PISprint IDFKeyboard IDKey_LeftShift 0 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect1 IDFKeyboard IDKey_1 10000 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect2 IDFKeyboard IDKey_2 10000 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect3 IDFKeyboard IDKey_3 10000 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect4 IDFKeyboard IDKey_4 10000 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect5 IDFKeyboard IDKey_5 10000 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect6 IDFKeyboard IDKey_6 10000 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect7 IDFKeyboard IDKey_7 10000 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect8 IDFKeyboard IDKey_8 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect1 IDFKeyboard IDKey_F1 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect2 IDFKeyboard IDKey_F2 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect3 IDFKeyboard IDKey_F3 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect4 IDFKeyboard IDKey_F4 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect5 IDFKeyboard IDKey_F5 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect6 IDFKeyboard IDKey_F6 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect7 IDFKeyboard IDKey_F7 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect8 IDFKeyboard IDKey_F8 10000 0
ControlMap.addButtonToTriggerMapping c_PIAltFire IDFMouse IDButton_1 0 0
ControlMap.addKeyToTriggerMapping c_PIDrop IDFKeyboard IDKey_G 10000 0
ControlMap.addKeyToTriggerMapping c_PILie IDFKeyboard IDKey_Z 0 0
ControlMap.addButtonToTriggerMapping c_PILie IDFMouse IDButton_3 0 1
ControlMap.addKeyToTriggerMapping c_PIToggleWeapon IDFKeyboard IDKey_F 10000 0
ControlMap.mouseSensitivity 1

ControlMap.create LandPlayerInputControlMap
ControlMap.addKeysToAxisMapping c_PIYaw IDFKeyboard IDKey_D IDKey_A 0
ControlMap.addAxisToAxisMapping c_PIPitch IDFMouse IDAxis_1 0 0
ControlMap.addAxisToAxisMapping c_PIRoll IDFMouse IDAxis_0 0 0
ControlMap.addKeysToAxisMapping c_PIThrottle IDFKeyboard IDKey_W IDKey_S 0
ControlMap.addButtonToTriggerMapping c_PIFire IDFMouse IDButton_0 0 0
ControlMap.addKeyToTriggerMapping c_PIFire IDFKeyboard IDKey_Space 0 1
ControlMap.addKeyToTriggerMapping c_PIMouseLook IDFKeyboard IDKey_LeftCtrl 0 0
ControlMap.addKeyToTriggerMapping c_PISprint IDFKeyboard IDKey_LeftShift 0 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect1 IDFKeyboard IDKey_1 10000 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect2 IDFKeyboard IDKey_2 10000 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect3 IDFKeyboard IDKey_3 10000 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect4 IDFKeyboard IDKey_4 10000 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect5 IDFKeyboard IDKey_5 10000 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect6 IDFKeyboard IDKey_6 10000 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect7 IDFKeyboard IDKey_7 10000 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect8 IDFKeyboard IDKey_8 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect1 IDFKeyboard IDKey_F1 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect2 IDFKeyboard IDKey_F2 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect3 IDFKeyboard IDKey_F3 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect4 IDFKeyboard IDKey_F4 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect5 IDFKeyboard IDKey_F5 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect6 IDFKeyboard IDKey_F6 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect7 IDFKeyboard IDKey_F7 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect8 IDFKeyboard IDKey_F8 10000 0
ControlMap.addButtonToTriggerMapping c_PIAltFire IDFMouse IDButton_1 0 0
ControlMap.addKeyToTriggerMapping c_PIAltFire IDFKeyboard IDKey_Numpad0 0 1
ControlMap.addKeyToTriggerMapping c_PICrouch IDFKeyboard IDKey_Z 0 0
ControlMap.addKeyToTriggerMapping c_PICameraMode1 IDFKeyboard IDKey_F9 10000 0
ControlMap.addKeyToTriggerMapping c_PICameraMode2 IDFKeyboard IDKey_F10 10000 0
ControlMap.addKeyToTriggerMapping c_PICameraMode3 IDFKeyboard IDKey_F11 10000 0
ControlMap.addKeyToTriggerMapping c_PICameraMode4 IDFKeyboard IDKey_F12 10000 0
ControlMap.addKeyToTriggerMapping c_PIToggleWeapon IDFKeyboard IDKey_F 10000 0
ControlMap.addKeyToTriggerMapping c_PIFlareFire IDFKeyboard IDKey_X 0 0

ControlMap.create AirPlayerInputControlMap
ControlMap.addKeysToAxisMapping c_PIYaw IDFKeyboard IDKey_D IDKey_A 0
ControlMap.addAxisToAxisMapping c_PIPitch IDFMouse IDAxis_1 0 0
ControlMap.addAxisToAxisMapping c_PIPitch IDFFalcon IDAxis_2 0 1
ControlMap.addAxisToAxisMapping c_PIRoll IDFMouse IDAxis_0 0 0
ControlMap.addKeysToAxisMapping c_PIThrottle IDFKeyboard IDKey_W IDKey_S 0
ControlMap.addButtonToTriggerMapping c_PIFire IDFMouse IDButton_0 0 0
ControlMap.addKeyToTriggerMapping c_PIFire IDFKeyboard IDKey_Space 0 1
ControlMap.addKeyToTriggerMapping c_PIMouseLook IDFKeyboard IDKey_LeftCtrl 0 0
ControlMap.addKeyToTriggerMapping c_PIAltSprint IDFKeyboard IDKey_W 1000 0
ControlMap.addKeyToTriggerMapping c_PISprint IDFKeyboard IDKey_LeftShift 0 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect1 IDFKeyboard IDKey_1 10000 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect2 IDFKeyboard IDKey_2 10000 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect3 IDFKeyboard IDKey_3 10000 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect4 IDFKeyboard IDKey_4 10000 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect5 IDFKeyboard IDKey_5 10000 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect6 IDFKeyboard IDKey_6 10000 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect7 IDFKeyboard IDKey_7 10000 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect8 IDFKeyboard IDKey_8 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect1 IDFKeyboard IDKey_F1 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect2 IDFKeyboard IDKey_F2 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect3 IDFKeyboard IDKey_F3 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect4 IDFKeyboard IDKey_F4 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect5 IDFKeyboard IDKey_F5 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect6 IDFKeyboard IDKey_F6 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect7 IDFKeyboard IDKey_F7 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect8 IDFKeyboard IDKey_F8 10000 0
ControlMap.addButtonToTriggerMapping c_PIAltFire IDFMouse IDButton_1 0 0
ControlMap.addKeyToTriggerMapping c_PIAltFire IDFKeyboard IDKey_Numpad0 0 1
ControlMap.addKeyToTriggerMapping c_PICameraMode1 IDFKeyboard IDKey_F9 10000 0
ControlMap.addKeyToTriggerMapping c_PICameraMode2 IDFKeyboard IDKey_F10 10000 0
ControlMap.addKeyToTriggerMapping c_PICameraMode3 IDFKeyboard IDKey_F11 10000 0
ControlMap.addKeyToTriggerMapping c_PICameraMode4 IDFKeyboard IDKey_F12 10000 0
ControlMap.addKeyToTriggerMapping c_PIToggleWeapon IDFKeyboard IDKey_F 10000 0
ControlMap.addKeyToTriggerMapping c_PIFlareFire IDFKeyboard IDKey_X 0 0
ControlMap.mouseSensitivity 1.7

ControlMap.create HelicopterPlayerInputControlMap
ControlMap.addKeysToAxisMapping c_PIYaw IDFKeyboard IDKey_D IDKey_A 0
ControlMap.addAxisToAxisMapping c_PIPitch IDFMouse IDAxis_1 0 0
ControlMap.addAxisToAxisMapping c_PIPitch IDFFalcon IDAxis_2 0 1
ControlMap.addAxisToAxisMapping c_PIRoll IDFMouse IDAxis_0 0 0
ControlMap.addAxisToAxisMapping c_PIRoll IDFFalcon IDAxis_0 0 1
ControlMap.addKeysToAxisMapping c_PIThrottle IDFKeyboard IDKey_W IDKey_S 0
ControlMap.addButtonToTriggerMapping c_PIFire IDFMouse IDButton_0 0 0
ControlMap.addKeyToTriggerMapping c_PIFire IDFKeyboard IDKey_Space 0 1
ControlMap.addKeyToTriggerMapping c_PIMouseLook IDFKeyboard IDKey_LeftCtrl 0 0
ControlMap.addKeyToTriggerMapping c_PIAltSprint IDFKeyboard IDKey_W 1000 0
ControlMap.addKeyToTriggerMapping c_PISprint IDFKeyboard IDKey_LeftShift 0 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect1 IDFKeyboard IDKey_1 10000 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect2 IDFKeyboard IDKey_2 10000 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect3 IDFKeyboard IDKey_3 10000 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect4 IDFKeyboard IDKey_4 10000 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect5 IDFKeyboard IDKey_5 10000 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect6 IDFKeyboard IDKey_6 10000 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect7 IDFKeyboard IDKey_7 10000 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect8 IDFKeyboard IDKey_8 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect1 IDFKeyboard IDKey_F1 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect2 IDFKeyboard IDKey_F2 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect3 IDFKeyboard IDKey_F3 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect4 IDFKeyboard IDKey_F4 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect5 IDFKeyboard IDKey_F5 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect6 IDFKeyboard IDKey_F6 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect7 IDFKeyboard IDKey_F7 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect8 IDFKeyboard IDKey_F8 10000 0
ControlMap.addButtonToTriggerMapping c_PIAltFire IDFMouse IDButton_1 0 0
ControlMap.addKeyToTriggerMapping c_PIAltFire IDFKeyboard IDKey_Numpad0 0 1
ControlMap.addKeyToTriggerMapping c_PICameraMode1 IDFKeyboard IDKey_F9 10000 0
ControlMap.addKeyToTriggerMapping c_PICameraMode2 IDFKeyboard IDKey_F10 10000 0
ControlMap.addKeyToTriggerMapping c_PICameraMode3 IDFKeyboard IDKey_F11 10000 0
ControlMap.addKeyToTriggerMapping c_PICameraMode4 IDFKeyboard IDKey_F12 10000 0
ControlMap.addKeyToTriggerMapping c_PIToggleWeapon IDFKeyboard IDKey_F 10000 0
ControlMap.addKeyToTriggerMapping c_PIFlareFire IDFKeyboard IDKey_X 0 0
ControlMap.mouseSensitivity 3

ControlMap.create SeaPlayerInputControlMap
ControlMap.addKeysToAxisMapping c_PIYaw IDFKeyboard IDKey_D IDKey_A 0
ControlMap.addKeysToAxisMapping c_PIPitch IDFKeyboard IDKey_ArrowUp IDKey_ArrowDown 0
ControlMap.addKeysToAxisMapping c_PIThrottle IDFKeyboard IDKey_W IDKey_S 0
ControlMap.addButtonToTriggerMapping c_PIFire IDFMouse IDButton_0 0 0
ControlMap.addKeyToTriggerMapping c_PIFire IDFKeyboard IDKey_Space 0 1
ControlMap.addKeyToTriggerMapping c_PISprint IDFKeyboard IDKey_LeftShift 0 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect1 IDFKeyboard IDKey_1 10000 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect2 IDFKeyboard IDKey_2 10000 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect3 IDFKeyboard IDKey_3 10000 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect4 IDFKeyboard IDKey_4 10000 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect5 IDFKeyboard IDKey_5 10000 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect6 IDFKeyboard IDKey_6 10000 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect7 IDFKeyboard IDKey_7 10000 0
ControlMap.addKeyToTriggerMapping c_PIWeaponSelect8 IDFKeyboard IDKey_8 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect1 IDFKeyboard IDKey_F1 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect2 IDFKeyboard IDKey_F2 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect3 IDFKeyboard IDKey_F3 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect4 IDFKeyboard IDKey_F4 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect5 IDFKeyboard IDKey_F5 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect6 IDFKeyboard IDKey_F6 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect7 IDFKeyboard IDKey_F7 10000 0
ControlMap.addKeyToTriggerMapping c_PIPositionSelect8 IDFKeyboard IDKey_F8 10000 0
ControlMap.addButtonToTriggerMapping c_PIAltFire IDFMouse IDButton_1 0 0
ControlMap.addKeyToTriggerMapping c_PIAltFire IDFKeyboard IDKey_Numpad0 0 1
ControlMap.addKeyToTriggerMapping c_PICameraMode1 IDFKeyboard IDKey_F9 10000 0
ControlMap.addKeyToTriggerMapping c_PICameraMode2 IDFKeyboard IDKey_F10 10000 0
ControlMap.addKeyToTriggerMapping c_PICameraMode3 IDFKeyboard IDKey_F11 10000 0
ControlMap.addKeyToTriggerMapping c_PICameraMode4 IDFKeyboard IDKey_F12 10000 0
ControlMap.addKeyToTriggerMapping c_PIToggleWeapon IDFKeyboard IDKey_F 10000 0

ControlMap.create defaultGameControlMap
ControlMap.addAxisToAxisMapping c_GIMouseLookX IDFMouse IDAxis_0 0 0
ControlMap.addAxisToAxisMapping c_GIMouseLookY IDFMouse IDAxis_1 0 0
ControlMap.addKeyToTriggerMapping c_GIMenu IDFKeyboard IDKey_Escape 10000 0
ControlMap.addKeyToTriggerMapping c_GIToggleConsole IDFKeyboard IDKey_Grave 10000 0
ControlMap.addKeyToTriggerMapping c_GIToggleConsole IDFKeyboard IDKey_End 10000 1
ControlMap.addKeyToTriggerMapping c_GIEscape IDFKeyboard IDKey_Escape 10000 0
ControlMap.addKeyToTriggerMapping c_GIUp IDFKeyboard IDKey_ArrowUp 10000 0
ControlMap.addKeyToTriggerMapping c_GIDown IDFKeyboard IDKey_ArrowDown 10000 0
ControlMap.addKeyToTriggerMapping c_GILeft IDFKeyboard IDKey_ArrowLeft 10000 0
ControlMap.addKeyToTriggerMapping c_GIRight IDFKeyboard IDKey_ArrowRight 10000 0
ControlMap.addKeyToTriggerMapping c_GIPageUp IDFKeyboard IDKey_PageUp 10000 0
ControlMap.addKeyToTriggerMapping c_GIPageDown IDFKeyboard IDKey_PageDown 10000 0
ControlMap.addKeyToTriggerMapping c_GIRightShift IDFKeyboard IDKey_RightShift 0 0
ControlMap.addKeyToTriggerMapping c_GILeftShift IDFKeyboard IDKey_LeftShift 0 0
ControlMap.addKeyToTriggerMapping c_GILeftCtrl IDFKeyboard IDKey_LeftCtrl 0 0
ControlMap.addKeyToTriggerMapping c_GIRightCtrl IDFKeyboard IDKey_RightCtrl 10000 0
ControlMap.addKeyToTriggerMapping c_GIRightAlt IDFKeyboard IDKey_RightAlt 0 0
ControlMap.addButtonToTriggerMapping c_GIOk IDFMouse IDButton_0 0 0
ControlMap.addButtonToTriggerMapping c_GIAltOk IDFMouse IDButton_1 0 0
ControlMap.addKeyToTriggerMapping c_GIScreenShot IDFKeyboard IDKey_PrintScreen 10000 0
ControlMap.addKeyToTriggerMapping c_GITogglePause IDFKeyboard IDKey_P 10000 0
ControlMap.addKeyToTriggerMapping c_GISayAll IDFKeyboard IDKey_J 10000 0
ControlMap.addKeyToTriggerMapping c_GISayTeam IDFKeyboard IDKey_K 10000 0
ControlMap.addKeyToTriggerMapping c_GISaySquad IDFKeyboard IDKey_L 10000 0
ControlMap.addAxisToTriggerMapping c_GIMouseWheelUp -1 IDFMouse IDAxis_2 0
ControlMap.addAxisToTriggerMapping -1 c_GIMouseWheelDown IDFMouse IDAxis_2 0
ControlMap.addKeyToTriggerMapping c_GITab IDFKeyboard IDKey_Tab 10000 0
ControlMap.addKeyToTriggerMapping c_GIEnter IDFKeyboard IDKey_Enter 10000 0
ControlMap.addKeyToTriggerMapping c_GIDelete IDFKeyboard IDKey_Delete 10000 0
ControlMap.addKeyToTriggerMapping c_GIBack IDFKeyboard IDKey_Backspace 10000 0
ControlMap.addKeyToTriggerMapping c_GITacticalComm IDFKeyboard IDKey_T 0 0
ControlMap.addKeyToTriggerMapping c_GIRadioComm IDFKeyboard IDKey_Q 0 0
ControlMap.addButtonToTriggerMapping c_GIRadioComm IDFMouse IDButton_2 0 1
ControlMap.addKeyToTriggerMapping c_GIMapSize IDFKeyboard IDKey_M 10000 0
ControlMap.addKeyToTriggerMapping c_GIMapZoom IDFKeyboard IDKey_N 10000 0
ControlMap.addKeyToTriggerMapping c_GIMenuSelect0 IDFKeyboard IDKey_0 10000 0
ControlMap.addKeyToTriggerMapping c_GIMenuSelect1 IDFKeyboard IDKey_1 10000 0
ControlMap.addKeyToTriggerMapping c_GIMenuSelect2 IDFKeyboard IDKey_2 10000 0
ControlMap.addKeyToTriggerMapping c_GIMenuSelect3 IDFKeyboard IDKey_3 10000 0
ControlMap.addKeyToTriggerMapping c_GIMenuSelect4 IDFKeyboard IDKey_4 10000 0
ControlMap.addKeyToTriggerMapping c_GIMenuSelect5 IDFKeyboard IDKey_5 10000 0
ControlMap.addKeyToTriggerMapping c_GIMenuSelect6 IDFKeyboard IDKey_6 10000 0
ControlMap.addKeyToTriggerMapping c_GIMenuSelect7 IDFKeyboard IDKey_7 10000 0
ControlMap.addKeyToTriggerMapping c_GIMenuSelect8 IDFKeyboard IDKey_8 10000 0
ControlMap.addKeyToTriggerMapping c_GIYes IDFKeyboard IDKey_PageUp 10000 0
ControlMap.addKeyToTriggerMapping c_GINo IDFKeyboard IDKey_PageDown 10000 0
ControlMap.addKeyToTriggerMapping c_GICreateSquad IDFKeyboard IDKey_Insert 10000 0
ControlMap.addKeyToTriggerMapping c_GIToggleFreeCamera IDFKeyboard IDKey_0 10000 0
ControlMap.addKeyToTriggerMapping c_GIHotRankUp IDFKeyboard IDKey_Add 10000 0
ControlMap.addKeyToTriggerMapping c_GIShowScoreboard IDFKeyboard IDKey_Tab 0 0
ControlMap.addKeyToTriggerMapping c_GIVoipUseLeaderChannel IDFKeyboard IDKey_B 0 0
ControlMap.addKeyToTriggerMapping c_GI3dMap IDFKeyboard IDKey_LeftAlt 10000 0
ControlMap.addKeyToTriggerMapping c_GISelectAll IDFKeyboard IDKey_A 10000 0
ControlMap.addKeyToTriggerMapping c_GIDeselectAll IDFKeyboard IDKey_D 10000 0
ControlMap.addKeyToTriggerMapping c_GILeader IDFKeyboard IDKey_Capital 10000 0
ControlMap.addKeyToTriggerMapping c_GILeader IDFKeyboard IDKey_Home 10000 1
ControlMap.addKeyToTriggerMapping c_GIVoipPushToTalk IDFKeyboard IDKey_V 0 0

ControlMap.create defaultPlayerInputControlMap
ControlMap.addAxisToAxisMapping c_PIMouseLookX IDFMouse IDAxis_0 0 0
ControlMap.addAxisToAxisMapping c_PIMouseLookX IDFFalcon IDAxis_0 0 1
ControlMap.addAxisToAxisMapping c_PIMouseLookY IDFMouse IDAxis_1 0 0
ControlMap.addAxisToAxisMapping c_PIMouseLookY IDFFalcon IDAxis_1 0 1
ControlMap.addKeyToTriggerMapping c_PIUse IDFKeyboard IDKey_E 0 0
ControlMap.addKeyToTriggerMapping c_PIReload IDFKeyboard IDKey_R 10000 0
ControlMap.addKeyToTriggerMapping c_PIToggleCameraMode IDFKeyboard IDKey_C 10000 0
ControlMap.addKeyToTriggerMapping c_PICrouch IDFKeyboard IDKey_LeftCtrl 0 0
ControlMap.addAxisToTriggerMapping c_PINextItem -1 IDFMouse IDAxis_2 0
ControlMap.addAxisToTriggerMapping -1 c_PIPrevItem IDFMouse IDAxis_2 0
""";

    private const string DefaultGeneral = """
GeneralSettings.setPlayedVOHelp "HUD_HELP_SPAWNSCREEN_KITTAB_pickArmyAndKit"
GeneralSettings.setSortOrder 0
GeneralSettings.setSortKey ""
GeneralSettings.setNumRoundsPlayed 0
GeneralSettings.setServerFilter ""
GeneralSettings.setHUDTransparency 255
GeneralSettings.setCrosshairColor 255 255 255 255
GeneralSettings.setBuddytagColor 0 0 0
GeneralSettings.setSquadtagColor 0 255 0
GeneralSettings.setMinimapRotate 1
GeneralSettings.setMinimapTransparency 170
GeneralSettings.setViewIntroMovie 1
GeneralSettings.setOutOfVoting 0
GeneralSettings.setBFTVSaveDirectory ""
GeneralSettings.setConfirmQuit 0
GeneralSettings.setMapIconAlphaTransparency 255
GeneralSettings.setMaxBots 64
GeneralSettings.setMaxBotsIncludeHumans 0
GeneralSettings.setBotSkill 0.5
GeneralSettings.setAutoScreenshot 0
GeneralSettings.setIgnoreBuddyRequests 0
GeneralSettings.setCameraShake 1
GeneralSettings.setColorBlindFriendly 0
GeneralSettings.setHelpPopups 0
GeneralSettings.setKillMessagesFilter 1
GeneralSettings.setRadioMessagesFilter 1
GeneralSettings.setChatMessagesFilter 1
GeneralSettings.setLastAwardsCheckDate 0
GeneralSettings.setAllowPunkBuster 1
GeneralSettings.setItemSelectionReverseItems 1
GeneralSettings.setToggleFilters 312194
GeneralSettings.setAutoReload 1
GeneralSettings.setConnectionType 5
GeneralSettings.setLCDDisplayModes 0
GeneralSettings.setConfirmLaunchEADownloader 0
GeneralSettings.setConfirmLaunchBFWeb 0
GeneralSettings.setConfirmDuplicateKey 0
GeneralSettings.setConfirmLogout 0
GeneralSettings.setConfirmDiscardBuddyMessages 0
GeneralSettings.setConfirmInstantAction 0
GeneralSettings.setConfirmReservedSlotsWarning 0
""";

    private const string DefaultDemoBookmarks = """

""";

    private const string DefaultMapList = """

""";

    private const string FallbackVideo = """
VideoSettings.setTerrainQuality 3
VideoSettings.setGeometryQuality 3
VideoSettings.setLightingQuality 3
VideoSettings.setDynamicLightingQuality 3
VideoSettings.setDynamicShadowsQuality 3
VideoSettings.setEffectsQuality 3
VideoSettings.setTextureQuality 3
VideoSettings.setTextureFilteringQuality 3
VideoSettings.setResolution 1920x1080@60Hz
VideoSettings.setAntialiasing 4Samples
VideoSettings.setViewDistanceScale 1
VideoSettings.setUseBloom 1
VideoSettings.setVideoOptionScheme 3
""";

    private const string FallbackAudio = """
AudioSettings.setVoipEnabled 1
AudioSettings.setVoipPlaybackVolume 1
AudioSettings.setVoipCaptureVolume 1
AudioSettings.setVoipCaptureThreshold 1
AudioSettings.setVoipBoostEnabled 1
AudioSettings.setVoipUsePushToTalk 1
AudioSettings.setProvider "software"
AudioSettings.setSoundQuality "High"
AudioSettings.setEffectsVolume 1
AudioSettings.setMusicVolume 1
AudioSettings.setHelpVoiceVolume 0
AudioSettings.setEnglishOnlyVoices 0
AudioSettings.setEnableEAX 1
""";

    private const string FallbackServerSettings = """
sv.serverName "Battlefield 2142"
sv.password ""
sv.internet 0
sv.bandwidthChoke 0
sv.serverIP ""
sv.serverPort 17567
sv.welcomeMessage ""
sv.punkBuster 0
sv.allowFreeCam 0
sv.allowExternalViews 1
sv.allowNoseCam 1
sv.hitIndicator 1
sv.maxPlayers 64
sv.numPlayersNeededToStart 1
sv.notEnoughPlayersRestartDelay 15
sv.startDelay 15
sv.endDelay 15
sv.spawnTime 15
sv.manDownTime 15
sv.endOfRoundDelay 3
sv.ticketRatio 100
sv.roundsPerMap 3
sv.timeLimit 0
sv.scoreLimit 0
sv.soldierFriendlyFire 100
sv.vehicleFriendlyFire 100
sv.soldierSplashFriendlyFire 100
sv.vehicleSplashFriendlyFire 100
sv.tkPunishEnabled 1
sv.tkNumPunishToKick 3
sv.tkPunishByDefault 0
sv.votingEnabled 1
sv.voteTime 90
sv.minPlayersForVoting 2
sv.gameSpyPort 29900
sv.allowNATNegotiation 0
sv.interfaceIP 
sv.autoRecord 0
sv.demoIndexURL "http://"
sv.demoDownloadURL "http://"
sv.autoDemoHook "adminutils/demo/rotate_demo.exe"
sv.demoQuality 1
sv.adminScript "default"
sv.timeBeforeRestartMap 30
sv.autoBalanceTeam 0
sv.teamRatioPercent 100
sv.voipEnabled 1
sv.voipQuality 5
sv.voipServerRemote 0
sv.voipServerRemoteIP 
sv.voipServerPort 55125
sv.voipBFClientPort 55123
sv.voipBFServerPort 55124
sv.voipSharedPassword 
sv.useGlobalRank 1
sv.useGlobalUnlocks 1
sv.minUnlockLevel 0
sv.maxUnlockLevel -1
sv.sponsorText ""
sv.sponsorLogoURL ""
sv.communityLogoURL ""
sv.customMapURL ""
sv.radioSpamInterval 6
sv.radioMaxSpamFlagCount 6
sv.radioBlockedDurationTime 30
sv.numReservedSlots 0
sv.maxRank 0
sv.friendlyFireWithMines 40
sv.botSkill 0.5
sv.allowSpectators 0
sv.allowTitanMovement 1
""";

    public Task SwitchAsync(string currentClientId, string targetClientId)
    {
        ValidateClientId(currentClientId);
        ValidateClientId(targetClientId);

        Directory.CreateDirectory(StoragePath);
        EnsureGeneratedFiles(ProfilesPath);
        SaveSnapshot(GetClientPath(currentClientId));

        if (currentClientId.Equals(targetClientId, StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask;

        string targetPath = GetClientPath(targetClientId);
        if (!Directory.Exists(targetPath))
        {
            Directory.CreateDirectory(targetPath);
            EnsureGeneratedFiles(targetPath);
            Logger.Info($"Initial profile generated for client '{targetClientId}'");
        }

        RestoreSnapshot(targetPath);
        EnsureGeneratedFiles(ProfilesPath);

        Logger.Info($"Profiles switched: '{currentClientId}' -> '{targetClientId}'");
        return Task.CompletedTask;
    }

    private string GetClientPath(string clientId) => Path.Combine(StoragePath, clientId);

    private static void ValidateClientId(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId) ||
            clientId is "." or ".." ||
            clientId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            clientId.Contains('/') || clientId.Contains('\\'))
            throw new ArgumentException("Недопустимый ID клиента.", nameof(clientId));
    }

    private static void EnsureGeneratedFiles(string root)
    {
        EnsureConfig(root, "Global.con", DefaultGlobal);

        string profile = Path.Combine(root, "0001");
        EnsureConfig(profile, "Profile.con", DefaultProfile);
        EnsureConfig(profile, "Video.con", DefaultVideo);
        EnsureConfig(profile, "Audio.con", DefaultAudio);
        EnsureConfig(profile, "ServerSettings.con", DefaultServerSettings);
        EnsureConfig(profile, "Controls.con", DefaultControls);
        EnsureConfig(profile, "General.con", DefaultGeneral);
        EnsureConfig(profile, "DemoBookmarks.con", DefaultDemoBookmarks);
        EnsureConfig(profile, "mapList.con", DefaultMapList);

        string fallback = Path.Combine(root, "Default");
        EnsureConfig(fallback, "Video.con", FallbackVideo);
        EnsureConfig(fallback, "Audio.con", FallbackAudio);
        EnsureConfig(fallback, "ServerSettings.con", FallbackServerSettings);
    }

    private static void EnsureConfig(string directory, string fileName, string content)
    {
        string path = Path.Combine(directory, fileName);
        if (File.Exists(path))
            return;

        Directory.CreateDirectory(directory);
        File.WriteAllText(path, content.Length == 0 ? string.Empty : content + Environment.NewLine);
        Logger.Info($"Config created: '{path}'");
    }

    private void SaveSnapshot(string destination)
    {
        string temp = destination + ".tmp";
        string old = destination + ".old";

        DeleteDirectory(temp);
        DeleteDirectory(old);
        CopyDirectory(ProfilesPath, temp);

        if (Directory.Exists(destination))
            Directory.Move(destination, old);

        try
        {
            Directory.Move(temp, destination);
            DeleteDirectory(old);
        }
        catch
        {
            if (!Directory.Exists(destination) && Directory.Exists(old))
                Directory.Move(old, destination);
            throw;
        }
    }

    private void RestoreSnapshot(string source)
    {
        string temp = ProfilesPath + ".nbl-temp";
        string old = ProfilesPath + ".nbl-old";

        DeleteDirectory(temp);
        DeleteDirectory(old);
        CopyDirectory(source, temp);

        if (Directory.Exists(ProfilesPath))
            Directory.Move(ProfilesPath, old);

        try
        {
            Directory.Move(temp, ProfilesPath);
            DeleteDirectory(old);
        }
        catch
        {
            if (!Directory.Exists(ProfilesPath) && Directory.Exists(old))
                Directory.Move(old, ProfilesPath);
            throw;
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (string dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));

        foreach (string file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, true);
    }
}
