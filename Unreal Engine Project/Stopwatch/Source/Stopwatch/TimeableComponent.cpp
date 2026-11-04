// Copyright Epic Games, Inc. All Rights Reserved.

#include "TimeableComponent.h"
#include "Components/MeshComponent.h"
#include "Components/PrimitiveComponent.h"
#include "Engine/World.h"
#include "GameFramework/Actor.h"
#include "Materials/MaterialInstanceDynamic.h"
#include "Materials/MaterialInterface.h"
#include "Stopwatch.h"
#include "TimerManager.h"
#include "TimeStateProvider.h"

UTimeableComponent::UTimeableComponent()
{
	PrimaryComponentTick.bCanEverTick = true;

	FrozenOverlayMaterial = TSoftObjectPtr<UMaterialInterface>(FSoftObjectPath(TEXT("/Game/Stopwatch/Materials/M_FrozenOverlay.M_FrozenOverlay")));
	RewindOverlayMaterial = TSoftObjectPtr<UMaterialInterface>(FSoftObjectPath(TEXT("/Game/Stopwatch/Materials/M_RewindOverlay.M_RewindOverlay")));
}

void UTimeableComponent::BeginPlay()
{
	Super::BeginPlay();

	TInlineComponentArray<UMeshComponent*> Meshes(GetOwner());

	for (UMeshComponent* Mesh : Meshes)
	{
		if (const FTransform* Intact = IntactTransforms.Find(Mesh->GetFName()))
		{
			FTimeableAuthoredPose& Pose = AuthoredPoses.AddDefaulted_GetRef();
			Pose.Mesh = Mesh;
			Pose.Broken = Mesh->GetRelativeTransform();
			Pose.Intact = *Intact;
		}

		if (UMaterialInstanceDynamic* Material = MakeClockworkMaterial(Mesh))
		{
			ClockworkMaterials.Add(Material);
		}
	}
}

void UTimeableComponent::SetMaterialParameter(FName Name, float Value)
{
	for (UMaterialInstanceDynamic* Material : ClockworkMaterials)
	{
		Material->SetScalarParameterValue(Name, Value);
	}
}

void UTimeableComponent::SetMaterialParameter(FName Name, const FLinearColor& Value)
{
	for (UMaterialInstanceDynamic* Material : ClockworkMaterials)
	{
		Material->SetVectorParameterValue(Name, Value);
	}
}

void UTimeableComponent::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	Unfreeze();

	if (IsRewinding() && RewindMode == ERewindMode::Recorded)
	{
		EndPlayback(true);
	}

	Phase = EPhase::Idle;
	RestoreOverlays();
	RestoreBodies();

	Super::EndPlay(EndPlayReason);
}

void UTimeableComponent::Freeze(float Duration)
{
	if (IsRewinding())
	{
		if (RewindMode == ERewindMode::Recorded)
		{
			EndPlayback(true);
		}
		else
		{
			// a partly restored object can't stay in the past: it crumbles once it unfreezes
			Phase = EPhase::Crumbling;
			RestoreOverlays();
			OnRewindChanged.Broadcast(false);
		}
	}

	if (!bIsFrozen)
	{
		bIsFrozen = true;
		SavedTimeDilation = GetOwner()->CustomTimeDilation;
		GetOwner()->CustomTimeDilation = 0.0f;

		SaveAndStopBodies();
		SaveAndApplyOverlays(FrozenOverlayMaterial);

		OnFrozenChanged.Broadcast(true);
	}

	// world timers ignore the owner's CustomTimeDilation, so the countdown keeps running. A zero rate would never fire
	GetWorld()->GetTimerManager().SetTimer(UnfreezeTimer, this, &UTimeableComponent::Unfreeze, FMath::Max(Duration, 0.01f));
}

void UTimeableComponent::Unfreeze()
{
	if (!bIsFrozen)
	{
		return;
	}

	bIsFrozen = false;
	GetWorld()->GetTimerManager().ClearTimer(UnfreezeTimer);
	GetOwner()->CustomTimeDilation = SavedTimeDilation;

	// a held object stays kinematic until its hold ends
	if (!IsHolding())
	{
		RestoreBodies();
	}

	RestoreOverlays();

	OnFrozenChanged.Broadcast(false);
}

bool UTimeableComponent::CanBeRewound() const
{
	if (!bCanBeRewound || bIsFrozen)
	{
		return false;
	}

	if (RewindMode == ERewindMode::Recorded)
	{
		// samples are only added on change, so a second one means the owner moved
		return Phase == EPhase::Idle && SampleCount >= 2;
	}

	return (Phase == EPhase::Idle || Phase == EPhase::Crumbling) && !AuthoredPoses.IsEmpty();
}

bool UTimeableComponent::Rewind()
{
	if (!CanBeRewound())
	{
		return false;
	}

	if (RewindMode == ERewindMode::Recorded)
	{
		RecordSample();
		PlaybackCursor = SampleCount - 1;

		// stopping the owner's time stops its tick from fighting the playback
		SavedTimeDilation = GetOwner()->CustomTimeDilation;
		GetOwner()->CustomTimeDilation = 0.0f;

		SaveAndStopBodies();
	}

	Phase = EPhase::Rewinding;
	SaveAndApplyOverlays(RewindOverlayMaterial);

	OnRewindChanged.Broadcast(true);
	return true;
}

void UTimeableComponent::ClearHistory()
{
	OldestSample = 0;
	SampleCount = 0;
}

void UTimeableComponent::TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction)
{
	Super::TickComponent(DeltaTime, TickType, ThisTickFunction);

	if (RewindMode == ERewindMode::AuthoredPast)
	{
		TickAuthoredPast(DeltaTime);
		return;
	}

	// DeltaTime is scaled by the owner's dilation, which is 0 during playback
	const float WorldDelta = GetWorld()->GetDeltaSeconds();

	if (IsRewinding())
	{
		TickPlayback(WorldDelta);
		return;
	}

	if (IsHolding() && TickHold(DeltaTime))
	{
		RestoreBodies();
		Phase = EPhase::Idle;
	}

	if (!bIsFrozen && bCanBeRewound)
	{
		RecordTimer += WorldDelta;

		if (RecordTimer >= RecordInterval)
		{
			RecordTimer = FMath::Fmod(RecordTimer, RecordInterval);
			RecordSample();
		}
	}
}

void UTimeableComponent::RecordSample()
{
	FTimeableSample Sample;
	Sample.Transform = GetOwner()->GetActorTransform();

	if (const UPrimitiveComponent* Root = Cast<UPrimitiveComponent>(GetOwner()->GetRootComponent()); Root && Root->IsSimulatingPhysics())
	{
		Sample.LinearVelocity = Root->GetPhysicsLinearVelocity();
		Sample.AngularVelocity = Root->GetPhysicsAngularVelocityInDegrees();
	}

	if (const ITimeStateProvider* Provider = Cast<ITimeStateProvider>(GetOwner()))
	{
		Sample.TimeState = Provider->CaptureTimeState();
	}

	const int32 Capacity = FMath::Max(2, FMath::CeilToInt(RecordSeconds / RecordInterval));

	if (Samples.Num() != Capacity)
	{
		Samples.SetNum(Capacity);
		ClearHistory();
	}

	if (SampleCount > 0)
	{
		const FTimeableSample& Newest = GetSample(SampleCount - 1);

		if (Sample.Transform.GetLocation().Equals(Newest.Transform.GetLocation(), RecordTolerance)
			&& FMath::RadiansToDegrees(Sample.Transform.GetRotation().AngularDistance(Newest.Transform.GetRotation())) <= RecordTolerance
			&& FMath::IsNearlyEqual(Sample.TimeState, Newest.TimeState))
		{
			return;
		}
	}

	if (SampleCount < Capacity)
	{
		Samples[(OldestSample + SampleCount) % Capacity] = Sample;
		++SampleCount;
	}
	else
	{
		Samples[OldestSample] = Sample;
		OldestSample = (OldestSample + 1) % Capacity;
	}
}

void UTimeableComponent::ApplyPlayback()
{
	const int32 Index = FMath::FloorToInt(PlaybackCursor);
	const float Alpha = PlaybackCursor - Index;
	const FTimeableSample& From = GetSample(Index);
	const FTimeableSample& To = GetSample(FMath::Min(Index + 1, SampleCount - 1));

	FTransform Pose;
	Pose.Blend(From.Transform, To.Transform, Alpha);
	GetOwner()->SetActorTransform(Pose);

	if (ITimeStateProvider* Provider = Cast<ITimeStateProvider>(GetOwner()))
	{
		Provider->RestoreTimeState(FMath::Lerp(From.TimeState, To.TimeState, Alpha));
	}
}

void UTimeableComponent::TickPlayback(float DeltaTime)
{
	PlaybackCursor = FMath::Max(PlaybackCursor - DeltaTime / RecordInterval * RewindSpeed, 0.0f);
	ApplyPlayback();

	if (PlaybackCursor <= 0.0f)
	{
		EndPlayback(false);
	}
}

void UTimeableComponent::EndPlayback(bool bInterrupted)
{
	GetOwner()->CustomTimeDilation = SavedTimeDilation;
	RestoreOverlays();

	// everything after the cursor is now the future
	const int32 Index = FMath::FloorToInt(PlaybackCursor);
	const FTimeableSample Current = GetSample(Index);
	SampleCount = Index + 1;

	for (FTimeableSavedBody& Saved : SavedBodies)
	{
		const bool bIsRoot = Saved.Body == GetOwner()->GetRootComponent();
		Saved.LinearVelocity = bInterrupted && bIsRoot ? Current.LinearVelocity : FVector::ZeroVector;
		Saved.AngularVelocity = bInterrupted && bIsRoot ? Current.AngularVelocity : FVector::ZeroVector;
	}

	if (bInterrupted)
	{
		// the freeze that interrupted this restores the bodies when it ends
		Phase = EPhase::Idle;
	}
	else if (bHoldAfterRewind)
	{
		Phase = EPhase::Holding;
		HoldTimeLeft = HoldDuration;
	}
	else
	{
		RestoreBodies();
		Phase = EPhase::Idle;
	}

	OnRewindChanged.Broadcast(false);
}

void UTimeableComponent::TickAuthoredPast(float DeltaTime)
{
	if (IsRewinding())
	{
		AuthoredAlpha = FMath::Min(AuthoredAlpha + DeltaTime / RewindDuration, 1.0f);
		ApplyAuthoredPose();

		if (AuthoredAlpha >= 1.0f)
		{
			Phase = EPhase::Holding;
			HoldTimeLeft = HoldDuration;
			RestoreOverlays();
			OnRewindChanged.Broadcast(false);
		}
	}
	else if (IsHolding())
	{
		if (TickHold(DeltaTime))
		{
			Phase = EPhase::Crumbling;
		}
	}
	else if (Phase == EPhase::Crumbling)
	{
		AuthoredAlpha = FMath::Max(AuthoredAlpha - DeltaTime / CrumbleDuration, 0.0f);
		ApplyAuthoredPose();

		if (AuthoredAlpha <= 0.0f)
		{
			Phase = EPhase::Idle;
		}
	}
}

void UTimeableComponent::ApplyAuthoredPose()
{
	const float Alpha = FMath::SmoothStep(0.0f, 1.0f, AuthoredAlpha);

	for (const FTimeableAuthoredPose& Authored : AuthoredPoses)
	{
		if (UMeshComponent* Mesh = Authored.Mesh.Get())
		{
			FTransform Pose;
			Pose.Blend(Authored.Broken, Authored.Intact, Alpha);
			Mesh->SetRelativeTransform(Pose);
		}
	}
}

bool UTimeableComponent::TickHold(float DeltaTime)
{
	if (HoldDuration <= 0.0f)
	{
		return false;
	}

	HoldTimeLeft -= DeltaTime;
	return HoldTimeLeft <= 0.0f;
}

void UTimeableComponent::SaveAndStopBodies()
{
	TInlineComponentArray<UPrimitiveComponent*> Primitives(GetOwner());

	for (UPrimitiveComponent* Primitive : Primitives)
	{
		if (Primitive->IsSimulatingPhysics())
		{
			FTimeableSavedBody& Saved = SavedBodies.AddDefaulted_GetRef();
			Saved.Body = Primitive;
			Saved.LinearVelocity = Primitive->GetPhysicsLinearVelocity();
			Saved.AngularVelocity = Primitive->GetPhysicsAngularVelocityInDegrees();

			// this leaves collision on, so a frozen object can be stood on
			Primitive->SetSimulatePhysics(false);
		}
	}
}

void UTimeableComponent::RestoreBodies()
{
	for (const FTimeableSavedBody& Saved : SavedBodies)
	{
		if (UPrimitiveComponent* Primitive = Saved.Body.Get())
		{
			Primitive->SetSimulatePhysics(true);
			Primitive->SetPhysicsLinearVelocity(Saved.LinearVelocity);
			Primitive->SetPhysicsAngularVelocityInDegrees(Saved.AngularVelocity);
			Primitive->WakeAllRigidBodies();
		}
	}

	SavedBodies.Reset();
}

void UTimeableComponent::SaveAndApplyOverlays(const TSoftObjectPtr<UMaterialInterface>& Material)
{
	UMaterialInterface* Overlay = Material.LoadSynchronous();

	if (!Overlay)
	{
		static bool bWarned = false;

		if (!bWarned)
		{
			bWarned = true;
			UE_LOG(LogStopwatch, Warning, TEXT("Overlay material '%s' is missing. Run Scripts/create_frozen_overlay.py to create it."), *Material.ToString());
		}

		return;
	}

	TInlineComponentArray<UMeshComponent*> Meshes(GetOwner());

	for (UMeshComponent* Mesh : Meshes)
	{
		FTimeableSavedOverlay& Saved = SavedOverlays.AddDefaulted_GetRef();
		Saved.Mesh = Mesh;
		Saved.Overlay = Mesh->GetOverlayMaterial();

		Mesh->SetOverlayMaterial(Overlay);
	}
}

void UTimeableComponent::RestoreOverlays()
{
	for (const FTimeableSavedOverlay& Saved : SavedOverlays)
	{
		if (UMeshComponent* Mesh = Saved.Mesh.Get())
		{
			Mesh->SetOverlayMaterial(Saved.Overlay);
		}
	}

	SavedOverlays.Reset();
}
