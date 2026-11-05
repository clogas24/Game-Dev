// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "Blueprint/UserWidget.h"
#include "StopwatchMenuWidget.generated.h"

class UVerticalBox;
struct FPlaytestStats;

/**
 *  The title, pause and win screens. The widget tree is built in C++, there's no Widget Blueprint
 *  The owning AStopwatchPlayerController shows the screen for the game mode, and the buttons call the game mode
 */
UCLASS()
class UStopwatchMenuWidget : public UUserWidget
{
	GENERATED_BODY()

public:

	/** Name, premise, controls and how to start */
	void ShowTitle();

	/** Resume, Restart and Quit, with the folder the playtest logs go to */
	void ShowPause(const FString& LogFolder);

	/** The run's stats, how to play again, Quit and the log folder */
	void ShowWon(const FPlaytestStats& Totals, const FString& LogFolder);

	/** The screen's text, for tests */
	FString GetScreenText() const;

protected:

	/** Builds the panel */
	virtual void NativeOnInitialized() override;

private:

	UFUNCTION()
	void HandleResume();

	UFUNCTION()
	void HandleRestart();

	UFUNCTION()
	void HandleQuit();

	void AddText(const FText& Text, int32 FontSize, const FLinearColor& Color);
	class UButton* AddButton(const FText& Label, FName Handler);

	UPROPERTY()
	TObjectPtr<UVerticalBox> Rows;
};
