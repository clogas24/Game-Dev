// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "Blueprint/UserWidget.h"
#include "TimeAbilityComponent.h"
#include "StopwatchHUDWidget.generated.h"

class UCanvasPanel;
class UCanvasPanelSlot;
class UImage;
class UProgressBar;
class UTextBlock;
class UTimeSandComponent;

/**
 *  Crosshair and Time Sand bar. The widget tree is built in C++, there's no Widget Blueprint
 *  Driven entirely by the delegates of the components it's bound to
 */
UCLASS(Config=Game)
class UStopwatchHUDWidget : public UUserWidget
{
	GENERATED_BODY()

public:

	/** Diameter of the dot, not counting its outline. Slate units, so the engine's DPI scaling applies. The widget is created from C++, so override in DefaultGame.ini */
	UPROPERTY(EditAnywhere, Config, Category="Crosshair", meta=(ClampMin="1"))
	float DotSize = 6.0f;

	/** How much the dot grows when it pops */
	UPROPERTY(EditAnywhere, Config, Category="Crosshair", meta=(ClampMin="1"))
	float PopScale = 1.3f;

	/** How long a pop lasts */
	UPROPERTY(EditAnywhere, Config, Category="Crosshair", meta=(ClampMin="0.01"))
	float PopSeconds = 0.1f;

	float GetDotScale() const;
	FLinearColor GetDotColor() const;

	/** Connects the HUD to the possessed character's components and shows their current state */
	void BindTo(UTimeSandComponent* Sand, UTimeAbilityComponent* Ability);

protected:

	/** Builds the widget tree */
	virtual void NativeOnInitialized() override;

private:

	UFUNCTION()
	void HandleSandChanged(float Current, float Max);

	UFUNCTION()
	void HandleSpendFailed();

	UFUNCTION()
	void HandleAimStateChanged(ETimeAimState AimState);

	/** Grows the dot for PopSeconds */
	UFUNCTION()
	void Pop();

	/** Puts the dot back to its normal size */
	void EndPop();

	/** Restores the bar and dot colors after a failed spend */
	void EndFlash();

	/** Moves a tick mark to where the sand would be after paying Cost, hiding it if unaffordable */
	void UpdateCostTick(UImage* Tick, float Cost, float Current, float Max);

	UPROPERTY()
	TObjectPtr<UCanvasPanel> Canvas;

	UPROPERTY()
	TObjectPtr<UImage> Crosshair;

	UPROPERTY()
	TObjectPtr<UProgressBar> SandBar;

	UPROPERTY()
	TObjectPtr<UTextBlock> SandLabel;

	UPROPERTY()
	TObjectPtr<UImage> FreezeTick;

	UPROPERTY()
	TObjectPtr<UImage> RewindTick;

	float FreezeCost = 0.0f;
	float RewindCost = 0.0f;
	FLinearColor AimColor = FLinearColor::White;
	FTimerHandle FlashTimer;
	FTimerHandle PopTimer;
};
