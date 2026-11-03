// Copyright Epic Games, Inc. All Rights Reserved.

#include "StopwatchSettingsWidget.h"
#include "Blueprint/WidgetTree.h"
#include "Brushes/SlateRoundedBoxBrush.h"
#include "Components/Border.h"
#include "Components/CanvasPanel.h"
#include "Components/CanvasPanelSlot.h"
#include "Components/Slider.h"
#include "Components/TextBlock.h"
#include "Components/VerticalBox.h"
#include "Components/VerticalBoxSlot.h"
#include "StopwatchPlayerController.h"

namespace
{
	const FLinearColor PanelColor(0.02f, 0.02f, 0.03f, 0.9f);
	const FVector2D PanelSize(420.0f, 170.0f);
	const FMargin RowPadding(0.0f, 6.0f);
}

void UStopwatchSettingsWidget::NativeOnInitialized()
{
	Super::NativeOnInitialized();

	UCanvasPanel* Canvas = WidgetTree->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass());
	WidgetTree->RootWidget = Canvas;

	UBorder* Panel = WidgetTree->ConstructWidget<UBorder>(UBorder::StaticClass());
	Panel->SetBrush(FSlateRoundedBoxBrush(FLinearColor::White, 8.0f));
	Panel->SetBrushColor(PanelColor);
	Panel->SetPadding(FMargin(24.0f));
	UCanvasPanelSlot* PanelSlot = Canvas->AddChildToCanvas(Panel);
	PanelSlot->SetAnchors(FAnchors(0.5f, 0.5f));
	PanelSlot->SetAlignment(FVector2D(0.5f, 0.5f));
	PanelSlot->SetSize(PanelSize);

	UVerticalBox* Rows = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	Panel->SetContent(Rows);

	UTextBlock* Title = WidgetTree->ConstructWidget<UTextBlock>(UTextBlock::StaticClass());
	Title->SetText(NSLOCTEXT("Stopwatch", "SettingsTitle", "SETTINGS"));
	Rows->AddChildToVerticalBox(Title)->SetPadding(RowPadding);

	SensitivityLabel = WidgetTree->ConstructWidget<UTextBlock>(UTextBlock::StaticClass());
	Rows->AddChildToVerticalBox(SensitivityLabel)->SetPadding(RowPadding);

	// not focusable, so the keyboard stays with the game and the menu key still closes the menu
	SensitivitySlider = WidgetTree->ConstructWidget<USlider>(USlider::StaticClass());
	SensitivitySlider->IsFocusable = false;
	SensitivitySlider->SetStepSize(0.05f);
	SensitivitySlider->OnValueChanged.AddUniqueDynamic(this, &UStopwatchSettingsWidget::HandleSliderChanged);
	Rows->AddChildToVerticalBox(SensitivitySlider)->SetPadding(RowPadding);

	UTextBlock* Hint = WidgetTree->ConstructWidget<UTextBlock>(UTextBlock::StaticClass());
	Hint->SetText(NSLOCTEXT("Stopwatch", "SettingsHint", "Tab to close"));
	Hint->SetColorAndOpacity(FSlateColor(FLinearColor(0.6f, 0.6f, 0.6f, 1.0f)));
	Rows->AddChildToVerticalBox(Hint)->SetPadding(RowPadding);
}

void UStopwatchSettingsWidget::Show(float Sensitivity, float MinSensitivity, float MaxSensitivity)
{
	SensitivitySlider->SetMinValue(MinSensitivity);
	SensitivitySlider->SetMaxValue(MaxSensitivity);
	SensitivitySlider->SetValue(Sensitivity);
	UpdateLabel(Sensitivity);
}

void UStopwatchSettingsWidget::HandleSliderChanged(float Value)
{
	if (AStopwatchPlayerController* Controller = GetOwningPlayer<AStopwatchPlayerController>())
	{
		Controller->SetMouseSensitivity(Value);
		UpdateLabel(Controller->GetMouseSensitivity());
	}
}

void UStopwatchSettingsWidget::UpdateLabel(float Sensitivity)
{
	SensitivityLabel->SetText(FText::FromString(FString::Printf(TEXT("Mouse sensitivity   %.2fx"), Sensitivity)));
}
