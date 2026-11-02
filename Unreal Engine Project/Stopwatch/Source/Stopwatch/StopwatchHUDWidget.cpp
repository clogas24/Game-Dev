// Copyright Epic Games, Inc. All Rights Reserved.

#include "StopwatchHUDWidget.h"
#include "Blueprint/WidgetTree.h"
#include "Brushes/SlateRoundedBoxBrush.h"
#include "Components/CanvasPanel.h"
#include "Components/CanvasPanelSlot.h"
#include "Components/Image.h"
#include "Components/ProgressBar.h"
#include "Components/TextBlock.h"
#include "Engine/World.h"
#include "TimerManager.h"
#include "TimeSandComponent.h"

namespace
{
	const FLinearColor CrosshairDefault(1.0f, 1.0f, 1.0f, 0.9f);
	const FLinearColor CrosshairAffordable(0.3f, 0.8f, 1.0f, 1.0f);
	const FLinearColor CrosshairUnaffordable(0.25f, 0.25f, 0.25f, 0.8f);
	const FLinearColor SandColor(0.9f, 0.65f, 0.2f, 1.0f);
	const FLinearColor FlashColor(0.9f, 0.05f, 0.05f, 1.0f);

	const float CrosshairSize = 8.0f;
	const FVector2D BarPosition(32.0f, -56.0f);
	const FVector2D BarSize(320.0f, 24.0f);
	const float TickWidth = 2.0f;
	const float TickOverhang = 4.0f;
	const float FlashSeconds = 0.25f;
}

void UStopwatchHUDWidget::NativeOnInitialized()
{
	Super::NativeOnInitialized();

	SetVisibility(ESlateVisibility::HitTestInvisible);

	Canvas = WidgetTree->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass());
	WidgetTree->RootWidget = Canvas;

	const FAnchors BottomLeft(0.0f, 1.0f);

	Crosshair = WidgetTree->ConstructWidget<UImage>(UImage::StaticClass());
	Crosshair->SetBrush(FSlateRoundedBoxBrush(FLinearColor::White, CrosshairSize * 0.5f));
	Crosshair->SetColorAndOpacity(CrosshairDefault);
	UCanvasPanelSlot* CrosshairSlot = Canvas->AddChildToCanvas(Crosshair);
	CrosshairSlot->SetAnchors(FAnchors(0.5f, 0.5f));
	CrosshairSlot->SetAlignment(FVector2D(0.5f, 0.5f));
	CrosshairSlot->SetSize(FVector2D(CrosshairSize, CrosshairSize));

	SandBar = WidgetTree->ConstructWidget<UProgressBar>(UProgressBar::StaticClass());
	SandBar->SetFillColorAndOpacity(SandColor);
	UCanvasPanelSlot* BarSlot = Canvas->AddChildToCanvas(SandBar);
	BarSlot->SetAnchors(BottomLeft);
	BarSlot->SetPosition(BarPosition);
	BarSlot->SetSize(BarSize);

	SandLabel = WidgetTree->ConstructWidget<UTextBlock>(UTextBlock::StaticClass());
	SandLabel->SetColorAndOpacity(FSlateColor(FLinearColor::White));
	UCanvasPanelSlot* LabelSlot = Canvas->AddChildToCanvas(SandLabel);
	LabelSlot->SetAnchors(BottomLeft);
	LabelSlot->SetAlignment(FVector2D(0.0f, 1.0f));
	LabelSlot->SetPosition(BarPosition - FVector2D(0.0f, TickOverhang));
	LabelSlot->SetAutoSize(true);

	for (TObjectPtr<UImage>* Tick : { &FreezeTick, &RewindTick })
	{
		*Tick = WidgetTree->ConstructWidget<UImage>(UImage::StaticClass());
		UCanvasPanelSlot* TickSlot = Canvas->AddChildToCanvas(*Tick);
		TickSlot->SetAnchors(BottomLeft);
		TickSlot->SetSize(FVector2D(TickWidth, BarSize.Y + TickOverhang * 2.0f));
	}

	FreezeTick->SetColorAndOpacity(CrosshairAffordable);
	RewindTick->SetColorAndOpacity(FLinearColor::White);
}

void UStopwatchHUDWidget::BindTo(UTimeSandComponent* Sand, UTimeAbilityComponent* Ability)
{
	FreezeCost = Ability->FreezeSandCost;
	RewindCost = Ability->RewindSandCost;

	Sand->OnSandChanged.AddUniqueDynamic(this, &UStopwatchHUDWidget::HandleSandChanged);
	Sand->OnSpendFailed.AddUniqueDynamic(this, &UStopwatchHUDWidget::HandleSpendFailed);
	Ability->OnFreezeAimStateChanged.AddUniqueDynamic(this, &UStopwatchHUDWidget::HandleAimStateChanged);

	HandleSandChanged(Sand->GetCurrentSand(), Sand->MaxSand);
	HandleAimStateChanged(Ability->GetAimState());
}

void UStopwatchHUDWidget::HandleSandChanged(float Current, float Max)
{
	SandBar->SetPercent(Max > 0.0f ? Current / Max : 0.0f);
	SandLabel->SetText(FText::FromString(FString::Printf(TEXT("%d / %d"), FMath::RoundToInt(Current), FMath::RoundToInt(Max))));

	UpdateCostTick(FreezeTick, FreezeCost, Current, Max);
	UpdateCostTick(RewindTick, RewindCost, Current, Max);
}

void UStopwatchHUDWidget::UpdateCostTick(UImage* Tick, float Cost, float Current, float Max)
{
	const bool bAffordable = Max > 0.0f && Current >= Cost;
	Tick->SetVisibility(bAffordable ? ESlateVisibility::HitTestInvisible : ESlateVisibility::Collapsed);

	if (bAffordable)
	{
		const float X = BarPosition.X + BarSize.X * (Current - Cost) / Max - TickWidth * 0.5f;
		Cast<UCanvasPanelSlot>(Tick->Slot)->SetPosition(FVector2D(X, BarPosition.Y - TickOverhang));
	}
}

void UStopwatchHUDWidget::HandleSpendFailed()
{
	SandBar->SetFillColorAndOpacity(FlashColor);
	GetWorld()->GetTimerManager().SetTimer(FlashTimer, this, &UStopwatchHUDWidget::EndFlash, FlashSeconds);
}

void UStopwatchHUDWidget::EndFlash()
{
	SandBar->SetFillColorAndOpacity(SandColor);
}

void UStopwatchHUDWidget::HandleAimStateChanged(EFreezeAimState AimState)
{
	switch (AimState)
	{
	case EFreezeAimState::Affordable:
		Crosshair->SetColorAndOpacity(CrosshairAffordable);
		break;
	case EFreezeAimState::Unaffordable:
		Crosshair->SetColorAndOpacity(CrosshairUnaffordable);
		break;
	default:
		Crosshair->SetColorAndOpacity(CrosshairDefault);
		break;
	}
}
