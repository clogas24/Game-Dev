// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/PlayerController.h"
#include "InputCoreTypes.h"
#include "StopwatchPlayerController.generated.h"

class UInputAction;
class UInputMappingContext;
class UUserWidget;
class UStopwatchHUDWidget;
class UStopwatchSettingsWidget;

/**
 *  Basic PlayerController class for a first person game
 *  Manages input mappings
 */
UCLASS(abstract)
class AStopwatchPlayerController : public APlayerController
{
	GENERATED_BODY()
	
protected:

	/** Input Mapping Contexts */
	UPROPERTY(EditAnywhere, Category ="Input|Input Mappings")
	TArray<UInputMappingContext*> DefaultMappingContexts;

	/** Input Mapping Contexts */
	UPROPERTY(EditAnywhere, Category="Input|Input Mappings")
	TArray<UInputMappingContext*> MobileExcludedMappingContexts;

	/** Mobile controls widget to spawn */
	UPROPERTY(EditAnywhere, Category="Input|Touch Controls")
	TSubclassOf<UUserWidget> MobileControlsWidgetClass;

	/** Pointer to the mobile controls widget */
	UPROPERTY()
	TObjectPtr<UUserWidget> MobileControlsWidget;

	/** If true, the player will use UMG touch controls even if not playing on mobile platforms */
	UPROPERTY(EditAnywhere, Config, Category = "Input|Touch Controls")
	bool bForceTouchControls = false;

	/** Keys that freeze the aimed target */
	UPROPERTY(EditAnywhere, Category="Input")
	TArray<FKey> FreezeKeys = { EKeys::LeftMouseButton, EKeys::Gamepad_RightTrigger };

	/** Keys that rewind */
	UPROPERTY(EditAnywhere, Category="Input")
	TArray<FKey> RewindKeys = { EKeys::RightMouseButton, EKeys::Gamepad_LeftTrigger };

	/** Keys that open and close the settings menu */
	UPROPERTY(EditAnywhere, Category="Input")
	TArray<FKey> SettingsMenuKeys = { EKeys::Tab };

	/** Lowest mouse sensitivity the settings menu offers */
	UPROPERTY(EditAnywhere, Category="Settings", meta=(ClampMin="0.01"))
	float MinMouseSensitivity = 0.1f;

	/** Highest mouse sensitivity the settings menu offers */
	UPROPERTY(EditAnywhere, Category="Settings", meta=(ClampMin="0.01"))
	float MaxMouseSensitivity = 3.0f;

	/** Lowest the player can look, in degrees */
	UPROPERTY(EditAnywhere, Category="Camera", meta=(ClampMin="-89", ClampMax="0"))
	float MinViewPitch = -85.0f;

	/** Highest the player can look, in degrees */
	UPROPERTY(EditAnywhere, Category="Camera", meta=(ClampMin="0", ClampMax="89"))
	float MaxViewPitch = 85.0f;

	/** Freeze input action, created at runtime so it needs no input assets */
	UPROPERTY(Transient)
	TObjectPtr<UInputAction> FreezeAction;

	/** Rewind input action, created at runtime so it needs no input assets */
	UPROPERTY(Transient)
	TObjectPtr<UInputAction> RewindAction;

	/** Settings menu input action, created at runtime so it needs no input assets */
	UPROPERTY(Transient)
	TObjectPtr<UInputAction> SettingsMenuAction;

	/** Maps the time ability keys to their actions */
	UPROPERTY(Transient)
	TObjectPtr<UInputMappingContext> TimeAbilityMappingContext;

	/** Debug input actions, only created outside Shipping. UHT doesn't allow these inside the #if */
	UPROPERTY(Transient)
	TObjectPtr<UInputAction> DebugOverlayAction;

	UPROPERTY(Transient)
	TObjectPtr<UInputAction> DebugRefillSandAction;

	UPROPERTY(Transient)
	TObjectPtr<UInputAction> DebugToggleViewAction;

	/** Crosshair and Time Sand bar */
	UPROPERTY()
	TObjectPtr<UStopwatchHUDWidget> HUDWidget;

	/** Created the first time the menu opens */
	UPROPERTY()
	TObjectPtr<UStopwatchSettingsWidget> SettingsWidget;

	/** Gameplay initialization */
	virtual void BeginPlay() override;

	/** Points the HUD at the possessed character */
	virtual void OnPossess(APawn* InPawn) override;

	/** Input mapping context setup */
	virtual void SetupInputComponent() override;

	/** Called for freeze input */
	void Freeze();

	/** Called for rewind input */
	void Rewind();

	/** Returns true if the player should use UMG touch controls */
	bool ShouldUseTouchControls() const;

#if !UE_BUILD_SHIPPING

	/** Draws the debug overlay */
	virtual void PlayerTick(float DeltaTime) override;

	/** F1 */
	void ToggleDebugOverlay() { bShowDebugOverlay = !bShowDebugOverlay; }

	/** F6 */
	void DebugRefillSand();

	/** V */
	void DebugToggleView();

	bool bShowDebugOverlay = false;

#endif

public:

	UStopwatchHUDWidget* GetHUDWidget() const { return HUDWidget; }

	/** Multiplier on mouse look, saved in GameUserSettings.ini when the menu closes */
	float GetMouseSensitivity() const { return MouseSensitivity; }

	/** Clamped to the menu's range */
	void SetMouseSensitivity(float NewSensitivity);

	/** Opens the settings menu and pauses, or closes it, saves and resumes */
	UFUNCTION(BlueprintCallable, Category="Settings")
	void ToggleSettingsMenu();

	bool IsSettingsMenuOpen() const { return bSettingsMenuOpen; }

private:

	float MouseSensitivity = 1.0f;
	bool bSettingsMenuOpen = false;
};
