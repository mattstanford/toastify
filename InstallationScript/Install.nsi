!include "DotNET.nsh"
!include LogicLib.nsh
!define DOTNET_VERSION "3.5"

; The name of the installer
Name "Toastify Installer"

; The file to write
OutFile "ToastifyInstaller.exe"

; The default installation directory
InstallDir $PROGRAMFILES\Toastify

; Request application privileges for Windows Vista
RequestExecutionLevel admin

;--------------------------------

; Pages

Page components
Page directory
Page instfiles

UninstPage uninstConfirm
UninstPage instfiles

;--------------------------------

Section "Toastify (required)"
  SectionIn RO
  
  !insertmacro CheckDotNET ${DOTNET_VERSION}
  
  ; Set output path to the installation directory.
  SetOutPath $INSTDIR
  
  ; Put file there
  File "Toastify.exe"	
  File "ToastifyApi.dll"
  File "ManagedWinapi.dll"
  File "Resources\ManagedWinapiNativeHelper.dll"
  File "WPFToolkit.dll"
  File "LICENSE"
  File "Newtonsoft.Json.dll"
  
  ; Write the uninstall keys for Windows
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Toastify" "DisplayName" "Toastify"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Toastify" "UninstallString" '"$INSTDIR\uninstall.exe"'
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Toastify" "DisplayIcon" "$INSTDIR\Toastify.exe,0"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Toastify" "Publisher" "Jesper Palm"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Toastify" "Version" "1.6"  
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Toastify" "DisplayVersion" "1.6"
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Toastify" "NoModify" 1
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Toastify" "NoRepair" 1
  WriteUninstaller "uninstall.exe"
SectionEnd

Section "Desktop icon"
  CreateShortCut "$DESKTOP\Toastify.lnk" "$INSTDIR\Toastify.exe" "" "$INSTDIR\Toastify.exe" 0
SectionEnd

Section "Start Menu icon"
  # Start Menu
  CreateShortCut "$SMPROGRAMS\Toastify.lnk" "$INSTDIR\Toastify.exe" "" "$INSTDIR\Toastify.exe" 0
  
SectionEnd

Section "Autostart"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "Toastify" '"$INSTDIR\Toastify.exe"'
SectionEnd

;--------------------------------

; Uninstaller

Section "Uninstall"
  
  # Remove Start Menu launcher
  Delete "$SMPROGRAMS\Toastify.lnk"
  
  ; Remove registry keys
  DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Toastify"
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "Toastify"

  ; Remove files and uninstaller
  Delete "$INSTDIR\Toastify.exe"
  Delete "$INSTDIR\ToastifyApi.dll"
  Delete "$INSTDIR\ManagedWinapi.dll"
  Delete "$INSTDIR\ManagedWinapiNativeHelper.dll"
  Delete "$INSTDIR\WPFToolkit.dll"
  Delete "$INSTDIR\LICENSE"
  Delete "$INSTDIR\Newtonsoft.Json.dll"
  
  ; remove the settings directory
  Delete "$APPDATA\Toastify.xml"
  RMDir "$APPDATA\Toastify"

  ; Remove shortcuts, if any
  Delete "$DESKTOP\Toastify.lnk"

  ; Remove directories used
  RMDir "$INSTDIR"
SectionEnd

Function .onInit
FunctionEnd