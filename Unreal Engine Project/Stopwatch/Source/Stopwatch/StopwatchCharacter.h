// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Character.h"
#include "Logging/LogMacros.h"
#include "StopwatchCharacter.generated.h"

class USpringArmComponent;
class UCameraComponent;
class UInputAction;
class UTimeAbilityComponent;
class UTimeSandComponent;
struct FInputActionValue;

DECLARE_LOG_CATEGORY_EXTERN(LogTemplateCharacter, Log, All);

/** What killed the player */
UENUM(BlueprintType)
enum class EDeathCause : uint8
{
	Fall,
	Crushed,
	Blade,
	Collapse
};

DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnPlayerDied, EDeathCause, Cause);

/**
 *  A simple player-controllable first person character
 *  Keeps the third person boom and camera, inactive, for comparing views during testing
 */
UCLASS(abstract)
class AStopwatchCharacter : public ACharacter
{
	GENERATED_BODY()

	/** Camera boom positioning the camera behind the character */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Components", meta = (AllowPrivateAccess = "true"))
	USpringArmComponent* CameraBoom;

	/** Follow camera */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Components", meta = (AllowPrivateAccess = "true"))
	UCameraComponent* FollowCamera;

	/** Eye camera. On the capsule rather than a head socket, so animation can't bob it */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Components", meta = (AllowPrivateAccess = "true"))
	UCameraComponent* FirstPersonCamera;

	/** The resource spent by time abilities */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Components", meta = (AllowPrivateAccess = "true"))
	UTimeSandComponent* TimeSand;

	/** Freeze and the other time abilities */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Components", meta = (AllowPrivateAccess = "true"))
	UTimeAbilityComponent* TimeAbility;

protected:

	/** Jump Input Action */
	UPROPERTY(EditAnywhere, Category="Input")
	UInputAction* JumpAction;

	/** Move Input Action */
	UPROPERTY(EditAnywhere, Category="Input")
	UInputAction* MoveAction;

	/** Look Input Action */
	UPROPERTY(EditAnywhere, Category="Input")
	UInputAction* LookAction;

	/** Mouse Look Input Action */
	UPROPERTY(EditAnywhere, Category="Input")
	UInputAction* MouseLookAction;

public:

	/** Which camera the player starts with */
	UPROPERTY(EditAnywhere, Category="Camera")
	bool bFirstPerson = true;

	/** Horizontal field of view of the first person camera */
	UPROPERTY(EditAnywhere, Category="Camera", meta=(ClampMin="30", ClampMax="150"))
	float FirstPersonFOV = 90.0f;

	/** Activates one camera and shows the body only when looking at it from outside */
	UFUNCTION(BlueprintCallable, Category="Camera")
	void SetFirstPerson(bool bNewFirstPerson);

	/** Seconds the screen takes to fade out on death, and again to fade back in */
	UPROPERTY(EditAnywhere, Category="Death", meta=(ClampMin="0.01"))
	float DeathFadeSeconds = 0.5f;

	/** Called once per death, when the player dies */
	UPROPERTY(BlueprintAssignable, Category="Death")
	FOnPlayerDied OnPlayerDied;

	/** The single entry point for death. Ignored while already dying */
	UFUNCTION(BlueprintCallable, Category="Death")
	void KillPlayer(EDeathCause Cause);

	UFUNCTION(BlueprintPure, Category="Death")
	bool IsDying() const { return bIsDying; }

	/** Kills any player overlapping the zone. Level pieces call this from their dilated tick, so a stopped hazard is harmless */
	static void KillOverlappingPlayer(const UPrimitiveComponent* Zone, EDeathCause Cause);

	/** Constructor */
	AStopwatchCharacter();	

protected:

	/** Applies the camera settings */
	virtual void BeginPlay() override;

	/** Initialize input action bindings */
	virtual void SetupPlayerInputComponent(class UInputComponent* PlayerInputComponent) override;

protected:

	/** Called for movement input */
	void Move(const FInputActionValue& Value);

	/** Called for looking input */
	void Look(const FInputActionValue& Value);

	/** Called for mouse looking input, scaled by the player's sensitivity setting */
	void MouseLook(const FInputActionValue& Value);

public:

	/** Handles move inputs from either controls or UI interfaces */
	UFUNCTION(BlueprintCallable, Category="Input")
	virtual void DoMove(float Right, float Forward);

	/** Handles look inputs from either controls or UI interfaces */
	UFUNCTION(BlueprintCallable, Category="Input")
	virtual void DoLook(float Yaw, float Pitch);

	/** Handles jump pressed inputs from either controls or UI interfaces */
	UFUNCTION(BlueprintCallable, Category="Input")
	virtual void DoJumpStart();

	/** Handles jump pressed inputs from either controls or UI interfaces */
	UFUNCTION(BlueprintCallable, Category="Input")
	virtual void DoJumpEnd();

public:

	/** Returns CameraBoom subobject **/
	FORCEINLINE class USpringArmComponent* GetCameraBoom() const { return CameraBoom; }

	/** Returns FollowCamera subobject **/
	FORCEINLINE class UCameraComponent* GetFollowCamera() const { return FollowCamera; }

	/** Returns FirstPersonCamera subobject **/
	FORCEINLINE UCameraComponent* GetFirstPersonCamera() const { return FirstPersonCamera; }

	/** Returns TimeSand subobject **/
	FORCEINLINE UTimeSandComponent* GetTimeSand() const { return TimeSand; }

	/** Returns TimeAbility subobject **/
	FORCEINLINE UTimeAbilityComponent* GetTimeAbility() const { return TimeAbility; }

private:

	/** TEMPORARY until checkpoints exist: teleports to the PlayerStart and refills the sand */
	void Respawn();

	bool bIsDying = false;
	FTimerHandle RespawnTimer;
};

