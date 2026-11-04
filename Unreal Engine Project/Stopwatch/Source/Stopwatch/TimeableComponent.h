// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "TimeableComponent.generated.h"

class UMaterialInstanceDynamic;
class UMaterialInterface;
class UMeshComponent;
class UPrimitiveComponent;

DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnFrozenChanged, bool, bFrozen);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnRewindChanged, bool, bRewinding);

/** How an actor goes back in time */
UENUM(BlueprintType)
enum class ERewindMode : uint8
{
	/** Plays back what actually happened to the actor */
	Recorded,

	/** Moves each mesh to an authored intact pose, holds, then crumbles back. For objects placed already broken */
	AuthoredPast
};

/** A simulating body's velocity, captured when it was frozen or started rewinding */
USTRUCT()
struct FTimeableSavedBody
{
	GENERATED_BODY()

	UPROPERTY()
	TWeakObjectPtr<UPrimitiveComponent> Body;

	FVector LinearVelocity = FVector::ZeroVector;
	FVector AngularVelocity = FVector::ZeroVector;
};

/** A mesh's overlay material, captured when it was frozen or started rewinding */
USTRUCT()
struct FTimeableSavedOverlay
{
	GENERATED_BODY()

	UPROPERTY()
	TWeakObjectPtr<UMeshComponent> Mesh;

	UPROPERTY()
	TObjectPtr<UMaterialInterface> Overlay;
};

/** One moment of the owner's recorded history */
struct FTimeableSample
{
	FTransform Transform;
	FVector LinearVelocity = FVector::ZeroVector;
	FVector AngularVelocity = FVector::ZeroVector;
	float TimeState = 0.0f;
};

/** A mesh's two poses in AuthoredPast mode */
USTRUCT()
struct FTimeableAuthoredPose
{
	GENERATED_BODY()

	UPROPERTY()
	TWeakObjectPtr<UMeshComponent> Mesh;

	FTransform Broken;
	FTransform Intact;
};

/**
 *  Add to an actor to make it a target for the player's time abilities
 *  Freezing stops the actor's time and physics, leaving its collision on, then resumes where it left off
 *  Rewinding takes it back through its past, see ERewindMode
 */
UCLASS(ClassGroup="Stopwatch", meta=(BlueprintSpawnableComponent))
class UTimeableComponent : public UActorComponent
{
	GENERATED_BODY()

public:

	/** Set to false to make this actor immune to Freeze */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Freeze")
	bool bCanBeFrozen = true;

	/** Overlay shown on every mesh of the actor while frozen */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Freeze")
	TSoftObjectPtr<UMaterialInterface> FrozenOverlayMaterial;

	/** Called when the actor freezes or resumes, but not when a freeze is only extended */
	UPROPERTY(BlueprintAssignable, Category="Freeze")
	FOnFrozenChanged OnFrozenChanged;

	/** Set to false to make this actor immune to Rewind */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Rewind")
	bool bCanBeRewound = true;

	/** How this actor goes back in time */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Rewind")
	ERewindMode RewindMode = ERewindMode::Recorded;

	/** Overlay shown on every mesh of the actor while rewinding */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Rewind")
	TSoftObjectPtr<UMaterialInterface> RewindOverlayMaterial;

	/** Seconds the actor stays restored, counted in its own dilated time. 0 means forever. Recorded mode only holds with bHoldAfterRewind */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Rewind", meta=(ClampMin="0"))
	float HoldDuration = 0.0f;

	/** World seconds between samples */
	UPROPERTY(EditAnywhere, Category="Rewind|Recorded", meta=(ClampMin="0.01"))
	float RecordInterval = 0.05f;

	/** Seconds of movement the buffer can hold. Time at rest isn't recorded, so a fallen object keeps its fall */
	UPROPERTY(EditAnywhere, Category="Rewind|Recorded", meta=(ClampMin="0.1"))
	float RecordSeconds = 8.0f;

	/** How far the actor must move, in units or degrees, to be worth another sample */
	UPROPERTY(EditAnywhere, Category="Rewind|Recorded", meta=(ClampMin="0"))
	float RecordTolerance = 1.0f;

	/** Playback speed compared to how fast it was recorded */
	UPROPERTY(EditAnywhere, Category="Rewind|Recorded", meta=(ClampMin="0.01"))
	float RewindSpeed = 1.5f;

	/** Stay kinematic for HoldDuration after a rewind, instead of restoring physics immediately */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Rewind|Recorded")
	bool bHoldAfterRewind = false;

	/** Intact relative transform of each mesh, by component name */
	UPROPERTY(EditAnywhere, Category="Rewind|Authored Past")
	TMap<FName, FTransform> IntactTransforms;

	/** Seconds to go from broken to intact */
	UPROPERTY(EditAnywhere, Category="Rewind|Authored Past", meta=(ClampMin="0.01"))
	float RewindDuration = 2.0f;

	/** Seconds to go from intact back to broken */
	UPROPERTY(EditAnywhere, Category="Rewind|Authored Past", meta=(ClampMin="0.01"))
	float CrumbleDuration = 1.0f;

	/** Called when a rewind starts or stops */
	UPROPERTY(BlueprintAssignable, Category="Rewind")
	FOnRewindChanged OnRewindChanged;

	/** Constructor */
	UTimeableComponent();

	UFUNCTION(BlueprintPure, Category="Freeze")
	virtual bool CanBeFrozen() const { return bCanBeFrozen; }

	UFUNCTION(BlueprintPure, Category="Freeze")
	bool IsFrozen() const { return bIsFrozen; }

	/** Freezes the actor, stopping any rewind in place. Freezing it again restarts the duration, which counts down in world time */
	UFUNCTION(BlueprintCallable, Category="Freeze")
	virtual void Freeze(float Duration);

	/** Resumes the actor, restoring its velocities and overlays */
	UFUNCTION(BlueprintCallable, Category="Freeze")
	virtual void Unfreeze();

	/** False if there's nothing meaningful to rewind: no movement in the buffer, already intact, already rewinding or frozen */
	UFUNCTION(BlueprintPure, Category="Rewind")
	bool CanBeRewound() const;

	UFUNCTION(BlueprintPure, Category="Rewind")
	bool IsRewinding() const { return Phase == EPhase::Rewinding; }

	/** Restored by a finished rewind and waiting out HoldDuration */
	UFUNCTION(BlueprintPure, Category="Rewind")
	bool IsHolding() const { return Phase == EPhase::Holding; }

	/** Starts rewinding if CanBeRewound */
	UFUNCTION(BlueprintCallable, Category="Rewind")
	bool Rewind();

	/** Forgets the recorded history. Call after teleporting the owner, so Rewind doesn't play through the jump */
	void ClearHistory();

	/** Sets a parameter of the clockwork material on every mesh of the owner. See MakeClockworkMaterial */
	void SetMaterialParameter(FName Name, float Value);
	void SetMaterialParameter(FName Name, const FLinearColor& Value);

	/** Seconds of history in the buffer */
	float GetBufferSeconds() const { return SampleCount * RecordInterval; }

	/** Seconds of hold left, or 0 for no hold or a forever one */
	float GetHoldTimeLeft() const { return IsHolding() ? HoldTimeLeft : 0.0f; }

	/** Records and plays back */
	virtual void TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction) override;

	/** Captures the broken pose of AuthoredPast meshes, and gives every mesh the clockwork look */
	virtual void BeginPlay() override;

	/** Clears the timer and restores the actor */
	virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) override;

private:

	enum class EPhase : uint8
	{
		Idle,
		Rewinding,
		Holding,
		Crumbling
	};

	void SaveAndStopBodies();
	void RestoreBodies();
	void SaveAndApplyOverlays(const TSoftObjectPtr<UMaterialInterface>& Material);
	void RestoreOverlays();

	/** Adds a sample if the owner changed since the last one */
	void RecordSample();

	const FTimeableSample& GetSample(int32 Index) const { return Samples[(OldestSample + Index) % Samples.Num()]; }

	/** Poses the owner between two samples */
	void ApplyPlayback();

	/** Advances a Recorded rewind. DeltaTime is undilated, because the owner's time is stopped during playback */
	void TickPlayback(float DeltaTime);

	/** Ends a Recorded rewind. An interrupted one stays where it is and resumes physics with the velocity it had at that moment */
	void EndPlayback(bool bInterrupted);

	/** Advances an AuthoredPast rewind and crumble. DeltaTime is dilated, so freezing pauses them */
	void TickAuthoredPast(float DeltaTime);

	void ApplyAuthoredPose();

	/** Counts down the hold in dilated time and returns true when it ends */
	bool TickHold(float DeltaTime);

	UPROPERTY(Transient)
	TArray<FTimeableSavedBody> SavedBodies;

	UPROPERTY(Transient)
	TArray<FTimeableSavedOverlay> SavedOverlays;

	UPROPERTY(Transient)
	TArray<FTimeableAuthoredPose> AuthoredPoses;

	UPROPERTY(Transient)
	TArray<TObjectPtr<UMaterialInstanceDynamic>> ClockworkMaterials;

	/** Ring buffer of the owner's history */
	TArray<FTimeableSample> Samples;
	int32 OldestSample = 0;
	int32 SampleCount = 0;

	bool bIsFrozen = false;
	float SavedTimeDilation = 1.0f;
	FTimerHandle UnfreezeTimer;

	EPhase Phase = EPhase::Idle;
	float RecordTimer = 0.0f;
	float PlaybackCursor = 0.0f;
	float HoldTimeLeft = 0.0f;

	/** 0 is broken, 1 is intact */
	float AuthoredAlpha = 0.0f;
};
