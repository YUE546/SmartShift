﻿﻿﻿﻿﻿﻿﻿﻿﻿; SmartShift Installer - Modern UI Edition
!include "MUI2.nsh"

!define APP_NAME "SmartShift"
!define APP_VERSION "1.0.0"
!define APP_EXE "SmartShift.UI.exe"
!define APP_PUBLISHER "SmartShift"
!define INSTALL_DIR "$PROGRAMFILES\${APP_NAME}"
!define INSTALLER_NAME "SmartShift-Setup-${APP_VERSION}.exe"

; 源文件目录：默认为相对于本脚本的编译输出目录；可通过命令行 /DSOURCE_DIR=... 覆盖
!ifndef SOURCE_DIR
    !define SOURCE_DIR "..\SmartShift.UI\bin\Release"
!endif

Name "${APP_NAME} ${APP_VERSION}"
Caption "${APP_NAME} Setup"
OutFile "${INSTALLER_NAME}"
Unicode True
InstallDir "${INSTALL_DIR}"
InstallDirRegKey HKLM "Software\${APP_NAME}" "InstallDir"
RequestExecutionLevel admin

VIProductVersion "1.0.0.0"
VIAddVersionKey "ProductName" "${APP_NAME}"
VIAddVersionKey "CompanyName" "${APP_PUBLISHER}"
VIAddVersionKey "LegalCopyright" "Copyright (C) 2026 ${APP_PUBLISHER}"
VIAddVersionKey "FileDescription" "${APP_NAME} Setup"
VIAddVersionKey "FileVersion" "${APP_VERSION}"
VIAddVersionKey "ProductVersion" "${APP_VERSION}"

!define MUI_ABORTWARNING
!define MUI_ICON "${SOURCE_DIR}\Assets\icon_light_app.ico"
!define MUI_UNICON "${SOURCE_DIR}\Assets\icon_dark_app.ico"

!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_INSTFILES

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "SimpChinese"

Section "Core Files" SEC_CORE
    SectionIn RO
    SetOutPath "$INSTDIR"
    File "${SOURCE_DIR}\${APP_EXE}"
    File "${SOURCE_DIR}\SmartShift.UI.exe.config"
    File "${SOURCE_DIR}\SmartShift.Core.dll"
    File "${SOURCE_DIR}\Newtonsoft.Json.dll"
    File "${SOURCE_DIR}\Serilog.dll"
    File "${SOURCE_DIR}\Serilog.Sinks.File.dll"
    File "${SOURCE_DIR}\Hardcodet.Wpf.TaskbarNotification.dll"

    SetOutPath "$INSTDIR\Assets"
    File "${SOURCE_DIR}\Assets\icon_light_app.ico"
    File "${SOURCE_DIR}\Assets\icon_dark_app.ico"
    File "${SOURCE_DIR}\Assets\icon_light_tray.ico"
    File "${SOURCE_DIR}\Assets\icon_dark_tray.ico"
    File "${SOURCE_DIR}\Assets\icon_light_hd.png"
    File "${SOURCE_DIR}\Assets\icon_dark_hd.png"

    CreateDirectory "$INSTDIR\logs"

    WriteRegStr HKLM "Software\${APP_NAME}" "Version" "${APP_VERSION}"
    WriteRegStr HKLM "Software\${APP_NAME}" "InstallDir" "$INSTDIR"

    ; 添加到"添加/删除程序"列表
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayName" "${APP_NAME}"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayVersion" "${APP_VERSION}"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "Publisher" "${APP_PUBLISHER}"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayIcon" "$INSTDIR\Assets\icon_light_app.ico"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "UninstallString" '"$INSTDIR\uninstall.exe"'
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "InstallLocation" "$INSTDIR"
    WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "NoModify" 1
    WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "NoRepair" 1

    WriteUninstaller "$INSTDIR\uninstall.exe"
SectionEnd

Section "Start Menu Shortcuts" SEC_STARTMENU
    CreateDirectory "$SMPROGRAMS\${APP_NAME}"
    CreateShortCut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" \
        "$INSTDIR\${APP_EXE}" "" "$INSTDIR\Assets\icon_light_app.ico" 0
    CreateShortCut "$SMPROGRAMS\${APP_NAME}\Uninstall ${APP_NAME}.lnk" \
        "$INSTDIR\uninstall.exe" "" "$INSTDIR\uninstall.exe" 0
SectionEnd

Section "Desktop Shortcut" SEC_DESKTOP
    CreateShortCut "$DESKTOP\${APP_NAME}.lnk" \
        "$INSTDIR\${APP_EXE}" "" "$INSTDIR\Assets\icon_light_app.ico" 0
SectionEnd

Section "Auto Start" SEC_AUTOSTART
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "${APP_NAME}" '"$INSTDIR\${APP_EXE}" --autostart'
SectionEnd

LangString DESC_CORE ${LANG_SIMPCHINESE} "Core program files"
LangString DESC_STARTMENU ${LANG_SIMPCHINESE} "Start menu shortcuts"
LangString DESC_DESKTOP ${LANG_SIMPCHINESE} "Desktop shortcut"
LangString DESC_AUTOSTART ${LANG_SIMPCHINESE} "Start automatically when Windows starts"

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
    !insertmacro MUI_DESCRIPTION_TEXT ${SEC_CORE} $(DESC_CORE)
    !insertmacro MUI_DESCRIPTION_TEXT ${SEC_STARTMENU} $(DESC_STARTMENU)
    !insertmacro MUI_DESCRIPTION_TEXT ${SEC_DESKTOP} $(DESC_DESKTOP)
    !insertmacro MUI_DESCRIPTION_TEXT ${SEC_AUTOSTART} $(DESC_AUTOSTART)
!insertmacro MUI_FUNCTION_DESCRIPTION_END

Section "Uninstall"
    ExecWait 'taskkill /F /IM "${APP_EXE}" /T' $R0
    Sleep 1000

    Delete "$INSTDIR\${APP_EXE}"
    Delete "$INSTDIR\SmartShift.Core.dll"
    Delete "$INSTDIR\Newtonsoft.Json.dll"
    Delete "$INSTDIR\Serilog.dll"
    Delete "$INSTDIR\Serilog.Sinks.File.dll"
    Delete "$INSTDIR\Hardcodet.Wpf.TaskbarNotification.dll"
    Delete "$INSTDIR\Assets\icon_light_app.ico"
    Delete "$INSTDIR\Assets\icon_dark_app.ico"
    Delete "$INSTDIR\Assets\icon_light_tray.ico"
    Delete "$INSTDIR\Assets\icon_dark_tray.ico"
    Delete "$INSTDIR\Assets\icon_light_hd.png"
    Delete "$INSTDIR\Assets\icon_dark_hd.png"
    Delete "$INSTDIR\uninstall.exe"
    Delete "$INSTDIR\SmartShift.UI.exe.config"
    Delete "$INSTDIR\*.pdb"
    Delete "$INSTDIR\*.xml"

    RMDir /r "$INSTDIR\logs"
    RMDir "$INSTDIR\Assets"
    RMDir "$INSTDIR"

    Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
    Delete "$SMPROGRAMS\${APP_NAME}\Uninstall ${APP_NAME}.lnk"
    RMDir "$SMPROGRAMS\${APP_NAME}"
    Delete "$DESKTOP\${APP_NAME}.lnk"

    DeleteRegKey HKLM "Software\${APP_NAME}"
    DeleteRegKey HKCU "Software\${APP_NAME}"
    DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"
    DeleteRegValue HKLM "Software\Microsoft\Windows\CurrentVersion\Run" "${APP_NAME}"
    DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "${APP_NAME}"

    MessageBox MB_YESNO|MB_ICONQUESTION "是否同时删除 SmartShift 的用户配置和日志？$\n$\n选择 $\"是$\" 将删除 %APPDATA%\SmartShift 目录。" IDNO skip_appdata
        RMDir /r "$APPDATA\${APP_NAME}"
    skip_appdata:
SectionEnd
