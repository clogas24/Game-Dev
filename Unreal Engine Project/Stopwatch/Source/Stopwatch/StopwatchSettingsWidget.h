// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "Blueprint/UserWidget.h"
#include "StopwatchSettingsWidget.generated.h"

class USlider;
class UTextBlock;

/**
 *  Settings menu with a mouse sensitivity slider. The widget tree is built in C++, there's no Widget Blueprint
 *  The owning AStopwatchPlayerController opens and closes it, and keeps the setting
 */
UCLASS()
class UStopwatchSettingsWidget : public UUserWidget
{
	GENERATED_BODY()

public:

	/** Shows the current sensitivity on the slider */
	void Show(float Sensitivity, float MinSensitivity, float MaxSensitivity);

protected:

	/** Builds the widget tree */
	virtual void NativeOnInitialized() override;

private:

	UFUNCTION()
	void HandleSliderChanged(float Value);

	void UpdateLabel(float Sensitivity);

	UPROPERTY()
	TObjectPtr<USlider> SensitivitySlider;

	UPROPERTY()
	TObjectPtr<UTextBlock> SensitivityLabel;
};
