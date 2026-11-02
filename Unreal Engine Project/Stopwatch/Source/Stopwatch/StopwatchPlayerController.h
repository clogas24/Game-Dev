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

/**
 *  Basic PlayerController class for a third person game
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

	/** Freeze input action, created at runtime so it needs no input assets */
	UPROPERTY(Transient)
	TObjectPtr<UInputAction> FreezeAction;

	/** Rewind input action, created at runtime so it needs no input assets */
	UPROPERTY(Transient)
	TObjectPtr<UInputAction> RewindAction;

	/** Maps the time ability keys to their actions */
	UPROPERTY(Transient)
	TObjectPtr<UInputMappingContext> TimeAbilityMappingContext;

	/** Crosshair and Time Sand bar */
	UPROPERTY()
	TObjectPtr<UStopwatchHUDWidget> HUDWidget;

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

};
