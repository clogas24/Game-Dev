// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "Blueprint/UserWidget.h"
#include "TimeAbilityComponent.h"
#include "StopwatchHUDWidget.generated.h"

class UBorder;
class UCanvasPanel;
class UCanvasPanelSlot;
class UImage;
class UProgressBar;
class UTextBlock;
class UTimeSandComponent;

/**
 *  Crosshair, Time Sand bar and controls hint. The widget tree is built in C++, there's no Widget Blueprint
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

	/** Seconds a section's name stays on screen */
	UPROPERTY(EditAnywhere, Config, Category="Section", meta=(ClampMin="0.1"))
	float SectionBannerSeconds = 2.0f;

	/** Shows the controls in the bottom right corner */
	UPROPERTY(EditAnywhere, Config, Category="Controls Hint")
	bool bShowControlsHint = true;

	float GetDotScale() const;
	FLinearColor GetDotColor() const;
	FText GetControlsHintText() const;

	/** Connects the HUD to the possessed character's components and shows their current state */
	void BindTo(UTimeSandComponent* Sand, UTimeAbilityComponent* Ability);

	/** Names the gamepad's buttons in the controls hint instead of the keyboard and mouse's */
	void SetUsingGamepad(bool bGamepad);

	/** Shows the name at the top for SectionBannerSeconds */
	void ShowSectionBanner(const FText& Name);

	/** Shows a prompt for Seconds. Its {freeze}, {rewind} and {jump} follow the device used last */
	void ShowTutorial(const FText& Prompt, float Seconds);

	/** The collapse countdown at the top: urgent turns it red, bigger and pulsing */
	void ShowCollapse(float SecondsLeft, bool bUrgent);
	void HideCollapse();

	/** Big text in the middle of the screen while respawning */
	void ShowDeathCause(const FText& Cause);
	void HideDeathCause();

	/** What's on screen, empty when hidden. For tests */
	FText GetSectionBannerText() const;
	FText GetTutorialText() const;
	FText GetCollapseText() const;
	FText GetDeathCauseText() const;

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

	UPROPERTY()
	TObjectPtr<UTextBlock> ControlsHint;

	UPROPERTY()
	TObjectPtr<UTextBlock> SectionBanner;

	UPROPERTY()
	TObjectPtr<UBorder> TutorialPanel;

	UPROPERTY()
	TObjectPtr<UTextBlock> TutorialText;

	UPROPERTY()
	TObjectPtr<UTextBlock> CollapseTimer;

	UPROPERTY()
	TObjectPtr<UTextBlock> DeathCause;

	/** The tutorial prompt before its keys are filled in */
	FText TutorialPrompt;
	FTimerHandle SectionTimer;
	FTimerHandle TutorialTimer;
	bool bCollapseUrgentStyle = false;

	float FreezeCost = 0.0f;
	float RewindCost = 0.0f;
	FLinearColor AimColor = FLinearColor::White;
	FTimerHandle FlashTimer;
	FTimerHandle PopTimer;
};
