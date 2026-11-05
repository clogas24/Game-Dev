// Copyright Epic Games, Inc. All Rights Reserved.

#include "StopwatchHUDWidget.h"
#include "Blueprint/WidgetTree.h"
#include "Components/Border.h"
#include "Brushes/SlateRoundedBoxBrush.h"
#include "Components/CanvasPanel.h"
#include "Components/CanvasPanelSlot.h"
#include "Components/Image.h"
#include "Components/ProgressBar.h"
#include "Components/TextBlock.h"
#include "Engine/World.h"
#include "StopwatchPlayerController.h"
#include "TimerManager.h"
#include "TimeSandComponent.h"

namespace
{
	const FLinearColor CrosshairDefault(1.0f, 1.0f, 1.0f, 0.9f);
	const FLinearColor CrosshairCanFreeze(0.3f, 0.8f, 1.0f, 1.0f);
	const FLinearColor CrosshairRewindOnly(1.0f, 0.6f, 0.1f, 1.0f);
	const FLinearColor CrosshairUnaffordable(0.25f, 0.25f, 0.25f, 0.8f);
	const FLinearColor SandColor(0.9f, 0.65f, 0.2f, 1.0f);
	const FLinearColor FlashColor(0.9f, 0.05f, 0.05f, 1.0f);

	const FLinearColor DotOutlineColor(0.0f, 0.0f, 0.0f, 0.85f);
	const float DotOutlineWidth = 1.0f;
	const FVector2D BarPosition(32.0f, -56.0f);
	const FVector2D BarSize(320.0f, 24.0f);
	const float TickWidth = 2.0f;
	const float TickOverhang = 4.0f;
	const float FlashSeconds = 0.25f;

	const FLinearColor HintColor(1.0f, 1.0f, 1.0f, 0.7f);
	const FVector2D HintPosition(-32.0f, -32.0f);
	const int32 HintFontSize = 14;

	const FLinearColor BannerColor(1.0f, 0.85f, 0.5f, 1.0f);
	const FLinearColor CollapseColor(1.0f, 0.75f, 0.3f, 1.0f);
	const FLinearColor CollapseUrgentColor(1.0f, 0.15f, 0.1f, 1.0f);
	const FLinearColor TutorialPanelColor(0.02f, 0.02f, 0.03f, 0.8f);
	const int32 BannerFontSize = 34;
	const int32 CollapseFontSize = 26;
	const int32 CollapseUrgentFontSize = 38;
	const int32 TutorialFontSize = 20;
	const int32 DeathFontSize = 56;

	void SetFontSize(UTextBlock* Text, int32 Size)
	{
		FSlateFontInfo Font = Text->GetFont();
		Font.Size = Size;
		Text->SetFont(Font);
	}

	/** Centered text, anchored at Anchor and hidden until shown */
	UTextBlock* AddCenteredText(UWidgetTree* Tree, UCanvasPanel* Canvas, const FAnchors& Anchor, const FVector2D& Offset, int32 Size, const FLinearColor& Color)
	{
		UTextBlock* Text = Tree->ConstructWidget<UTextBlock>(UTextBlock::StaticClass());
		SetFontSize(Text, Size);
		Text->SetColorAndOpacity(FSlateColor(Color));
		Text->SetJustification(ETextJustify::Center);
		Text->SetShadowOffset(FVector2D(2.0f, 2.0f));
		Text->SetShadowColorAndOpacity(FLinearColor(0.0f, 0.0f, 0.0f, 0.8f));
		Text->SetVisibility(ESlateVisibility::Collapsed);
		UCanvasPanelSlot* Slot = Canvas->AddChildToCanvas(Text);
		Slot->SetAnchors(Anchor);
		Slot->SetAlignment(FVector2D(0.5f, 0.5f));
		Slot->SetPosition(Offset);
		Slot->SetAutoSize(true);
		return Text;
	}
}

void UStopwatchHUDWidget::NativeOnInitialized()
{
	Super::NativeOnInitialized();

	SetVisibility(ESlateVisibility::HitTestInvisible);

	Canvas = WidgetTree->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass());
	WidgetTree->RootWidget = Canvas;

	const FAnchors BottomLeft(0.0f, 1.0f);

	Crosshair = WidgetTree->ConstructWidget<UImage>(UImage::StaticClass());

	// the outline is drawn inside the brush, and the tint colors the white fill but leaves the black outline black
	const float DotOuterSize = DotSize + DotOutlineWidth * 2.0f;
	Crosshair->SetBrush(FSlateRoundedBoxBrush(FLinearColor::White, DotOuterSize * 0.5f, DotOutlineColor, DotOutlineWidth));
	Crosshair->SetColorAndOpacity(CrosshairDefault);
	UCanvasPanelSlot* CrosshairSlot = Canvas->AddChildToCanvas(Crosshair);
	CrosshairSlot->SetAnchors(FAnchors(0.5f, 0.5f));
	CrosshairSlot->SetAlignment(FVector2D(0.5f, 0.5f));
	CrosshairSlot->SetSize(FVector2D(DotOuterSize, DotOuterSize));

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

	FreezeTick->SetColorAndOpacity(CrosshairCanFreeze);
	RewindTick->SetColorAndOpacity(FLinearColor::White);

	ControlsHint = WidgetTree->ConstructWidget<UTextBlock>(UTextBlock::StaticClass());
	ControlsHint->SetColorAndOpacity(FSlateColor(HintColor));
	FSlateFontInfo HintFont = ControlsHint->GetFont();
	HintFont.Size = HintFontSize;
	ControlsHint->SetFont(HintFont);
	ControlsHint->SetVisibility(bShowControlsHint ? ESlateVisibility::HitTestInvisible : ESlateVisibility::Collapsed);
	UCanvasPanelSlot* HintSlot = Canvas->AddChildToCanvas(ControlsHint);
	HintSlot->SetAnchors(FAnchors(1.0f, 1.0f));
	HintSlot->SetAlignment(FVector2D(1.0f, 1.0f));
	HintSlot->SetPosition(HintPosition);
	HintSlot->SetAutoSize(true);

	CollapseTimer = AddCenteredText(WidgetTree, Canvas, FAnchors(0.5f, 0.0f), FVector2D(0.0f, 48.0f), CollapseFontSize, CollapseColor);
	SectionBanner = AddCenteredText(WidgetTree, Canvas, FAnchors(0.5f, 0.0f), FVector2D(0.0f, 120.0f), BannerFontSize, BannerColor);
	DeathCause = AddCenteredText(WidgetTree, Canvas, FAnchors(0.5f, 0.5f), FVector2D(0.0f, -80.0f), DeathFontSize, FLinearColor::White);

	// above the sand bar, out of the way of the crosshair
	TutorialPanel = WidgetTree->ConstructWidget<UBorder>(UBorder::StaticClass());
	TutorialPanel->SetBrush(FSlateRoundedBoxBrush(FLinearColor::White, 8.0f));
	TutorialPanel->SetBrushColor(TutorialPanelColor);
	TutorialPanel->SetPadding(FMargin(20.0f, 14.0f));
	TutorialPanel->SetVisibility(ESlateVisibility::Collapsed);
	UCanvasPanelSlot* TutorialSlot = Canvas->AddChildToCanvas(TutorialPanel);
	TutorialSlot->SetAnchors(FAnchors(0.5f, 1.0f));
	TutorialSlot->SetAlignment(FVector2D(0.5f, 1.0f));
	TutorialSlot->SetPosition(FVector2D(0.0f, -140.0f));
	TutorialSlot->SetAutoSize(true);

	TutorialText = WidgetTree->ConstructWidget<UTextBlock>(UTextBlock::StaticClass());
	SetFontSize(TutorialText, TutorialFontSize);
	TutorialText->SetColorAndOpacity(FSlateColor(FLinearColor::White));
	TutorialPanel->SetContent(TutorialText);
	SetUsingGamepad(false);
}

void UStopwatchHUDWidget::SetUsingGamepad(bool bGamepad)
{
	FString Text = bGamepad ? TEXT("RT Freeze  ·  LT Rewind  ·  A Jump") : TEXT("LMB Freeze  ·  RMB Rewind  ·  Space Jump");

#if !UE_BUILD_SHIPPING
	Text += TEXT("  ·  F6 Refill Sand");
#endif

	ControlsHint->SetText(FText::FromString(Text));

	// the prompt names the keys of the device used last
	if (TutorialPanel->GetVisibility() != ESlateVisibility::Collapsed)
	{
		if (const AStopwatchPlayerController* Controller = GetOwningPlayer<AStopwatchPlayerController>())
		{
			TutorialText->SetText(Controller->FormatPrompt(TutorialPrompt, bGamepad));
		}
	}
}

FText UStopwatchHUDWidget::GetControlsHintText() const
{
	return ControlsHint->GetText();
}

void UStopwatchHUDWidget::BindTo(UTimeSandComponent* Sand, UTimeAbilityComponent* Ability)
{
	FreezeCost = Ability->FreezeSandCost;
	RewindCost = Ability->RewindSandCost;

	Sand->OnSandChanged.AddUniqueDynamic(this, &UStopwatchHUDWidget::HandleSandChanged);
	Sand->OnSpendFailed.AddUniqueDynamic(this, &UStopwatchHUDWidget::HandleSpendFailed);
	Ability->OnAimStateChanged.AddUniqueDynamic(this, &UStopwatchHUDWidget::HandleAimStateChanged);
	Ability->OnAbilityUsed.AddUniqueDynamic(this, &UStopwatchHUDWidget::Pop);

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
	Crosshair->SetColorAndOpacity(FlashColor);
	GetWorld()->GetTimerManager().SetTimer(FlashTimer, this, &UStopwatchHUDWidget::EndFlash, FlashSeconds);
}

void UStopwatchHUDWidget::EndFlash()
{
	SandBar->SetFillColorAndOpacity(SandColor);
	Crosshair->SetColorAndOpacity(AimColor);
}

void UStopwatchHUDWidget::HandleAimStateChanged(ETimeAimState AimState)
{
	switch (AimState)
	{
	case ETimeAimState::CanFreeze:
		AimColor = CrosshairCanFreeze;
		break;
	case ETimeAimState::RewindOnly:
		AimColor = CrosshairRewindOnly;
		break;
	case ETimeAimState::Unaffordable:
		AimColor = CrosshairUnaffordable;
		break;
	default:
		AimColor = CrosshairDefault;
		break;
	}

	// looking away mustn't cut a red tint short
	if (!GetWorld()->GetTimerManager().IsTimerActive(FlashTimer))
	{
		Crosshair->SetColorAndOpacity(AimColor);
	}

	Pop();
}

void UStopwatchHUDWidget::Pop()
{
	// a render transform, so the dot grows around the screen center without touching the layout
	Crosshair->SetRenderScale(FVector2D(PopScale, PopScale));
	GetWorld()->GetTimerManager().SetTimer(PopTimer, this, &UStopwatchHUDWidget::EndPop, PopSeconds);
}

void UStopwatchHUDWidget::EndPop()
{
	Crosshair->SetRenderScale(FVector2D(1.0f, 1.0f));
}

float UStopwatchHUDWidget::GetDotScale() const
{
	return Crosshair->GetRenderTransform().Scale.X;
}

FLinearColor UStopwatchHUDWidget::GetDotColor() const
{
	return Crosshair->GetColorAndOpacity();
}

void UStopwatchHUDWidget::ShowSectionBanner(const FText& Name)
{
	SectionBanner->SetText(Name);
	SectionBanner->SetVisibility(ESlateVisibility::HitTestInvisible);
	GetWorld()->GetTimerManager().SetTimer(SectionTimer, FTimerDelegate::CreateWeakLambda(this, [this]()
	{
		SectionBanner->SetVisibility(ESlateVisibility::Collapsed);
	}), SectionBannerSeconds, false);
}

void UStopwatchHUDWidget::ShowTutorial(const FText& Prompt, float Seconds)
{
	TutorialPrompt = Prompt;
	TutorialPanel->SetVisibility(ESlateVisibility::HitTestInvisible);

	if (const AStopwatchPlayerController* Controller = GetOwningPlayer<AStopwatchPlayerController>())
	{
		TutorialText->SetText(Controller->FormatPrompt(Prompt, Controller->IsUsingGamepad()));
	}

	GetWorld()->GetTimerManager().SetTimer(TutorialTimer, FTimerDelegate::CreateWeakLambda(this, [this]()
	{
		TutorialPanel->SetVisibility(ESlateVisibility::Collapsed);
	}), Seconds, false);
}

void UStopwatchHUDWidget::ShowDeathCause(const FText& Cause)
{
	DeathCause->SetText(Cause);
	DeathCause->SetVisibility(ESlateVisibility::HitTestInvisible);
}

void UStopwatchHUDWidget::HideDeathCause()
{
	DeathCause->SetVisibility(ESlateVisibility::Collapsed);
}

FText UStopwatchHUDWidget::GetSectionBannerText() const
{
	return SectionBanner->GetVisibility() == ESlateVisibility::Collapsed ? FText::GetEmpty() : SectionBanner->GetText();
}

FText UStopwatchHUDWidget::GetTutorialText() const
{
	return TutorialPanel->GetVisibility() == ESlateVisibility::Collapsed ? FText::GetEmpty() : TutorialText->GetText();
}

FText UStopwatchHUDWidget::GetCollapseText() const
{
	return CollapseTimer->GetVisibility() == ESlateVisibility::Collapsed ? FText::GetEmpty() : CollapseTimer->GetText();
}

FText UStopwatchHUDWidget::GetDeathCauseText() const
{
	return DeathCause->GetVisibility() == ESlateVisibility::Collapsed ? FText::GetEmpty() : DeathCause->GetText();
}

void UStopwatchHUDWidget::HideCollapse()
{
	CollapseTimer->SetVisibility(ESlateVisibility::Collapsed);
}

void UStopwatchHUDWidget::ShowCollapse(float SecondsLeft, bool bUrgent)
{
	const int32 Seconds = FMath::CeilToInt(SecondsLeft);
	CollapseTimer->SetText(FText::FromString(FString::Printf(TEXT("THE TOWER IS COLLAPSING   %d:%02d"), Seconds / 60, Seconds % 60)));
	CollapseTimer->SetVisibility(ESlateVisibility::HitTestInvisible);

	if (bUrgent != bCollapseUrgentStyle)
	{
		bCollapseUrgentStyle = bUrgent;
		SetFontSize(CollapseTimer, bUrgent ? CollapseUrgentFontSize : CollapseFontSize);
		CollapseTimer->SetColorAndOpacity(FSlateColor(bUrgent ? CollapseUrgentColor : CollapseColor));
	}

	// urgent: pulses twice a second
	CollapseTimer->SetRenderOpacity(bUrgent ? 0.65f + 0.35f * FMath::Cos(GetWorld()->GetTimeSeconds() * UE_TWO_PI * 2.0f) : 1.0f);
}
