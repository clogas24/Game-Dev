// Copyright Epic Games, Inc. All Rights Reserved.

#include "TimeableComponent.h"
#include "Components/MeshComponent.h"
#include "Components/PrimitiveComponent.h"
#include "Engine/World.h"
#include "GameFramework/Actor.h"
#include "Materials/MaterialInterface.h"
#include "Stopwatch.h"
#include "TimerManager.h"

UTimeableComponent::UTimeableComponent()
{
	FrozenOverlayMaterial = TSoftObjectPtr<UMaterialInterface>(FSoftObjectPath(TEXT("/Game/Stopwatch/Materials/M_FrozenOverlay.M_FrozenOverlay")));
}

void UTimeableComponent::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	Unfreeze();

	Super::EndPlay(EndPlayReason);
}

void UTimeableComponent::Freeze(float Duration)
{
	if (!bIsFrozen)
	{
		bIsFrozen = true;
		SavedTimeDilation = GetOwner()->CustomTimeDilation;
		GetOwner()->CustomTimeDilation = 0.0f;

		SaveAndStopBodies();
		SaveAndApplyOverlays();

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

	RestoreBodies();
	RestoreOverlays();

	OnFrozenChanged.Broadcast(false);
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

void UTimeableComponent::SaveAndApplyOverlays()
{
	UMaterialInterface* Overlay = FrozenOverlayMaterial.LoadSynchronous();

	if (!Overlay)
	{
		static bool bWarned = false;

		if (!bWarned)
		{
			bWarned = true;
			UE_LOG(LogStopwatch, Warning, TEXT("Frozen overlay material '%s' is missing. Run Scripts/create_frozen_overlay.py to create it."), *FrozenOverlayMaterial.ToString());
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
