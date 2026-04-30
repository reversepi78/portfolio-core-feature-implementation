using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Zombie : LivingEntity
{
    public LayerMask whatIsTarget; // 추적 대상 레이어

    LivingEntity targetEntity; // 추적대상
    NavMeshAgent navMeshAgent; // 경로 계산 AI 에이전트

    public ParticleSystem hitEffect; // 피격 시 재생할 파티클 효과
    public AudioClip deathSound; // 사망 시 재생할 소리
    public AudioClip hitSound; // 피격 시 재생할 소리

    Animator zombieAnimator; // 애니메이터 컴포넌트
    AudioSource zombieAudioPlayer; // 오디오 소스 컴포넌트
    Renderer zombieRenderer; // 렌더러 컴포넌트

    public float damage = 20f; // 공격력
    public float timeBetAttack = 0.5f; // 공격 간격
    float lastAttackTime; // 마지막 공격 시점

    public GameObject miniMapIcon { get; set; }

    // 추적할 대상이 존재하는지 알려주는 프로퍼티
    bool hasTarget
    {
        get
        {
            if(targetEntity != null && !targetEntity.dead) // 추적할 대상이 존재하고 대상이 죽지 않았다면
            {
                return true;
            }

            return false;
        }
    }

    private void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        zombieAnimator = GetComponent<Animator>();
        zombieAudioPlayer = GetComponent<AudioSource>();

        zombieRenderer = GetComponentInChildren<Renderer>();
    }

    [PunRPC]
    public void Setup(float newHealth, float newDamage, float newSpeed, Color skinColor)
    {
        startingHealth = newHealth;
        health = newHealth;

        damage = newDamage;

        navMeshAgent.speed = newSpeed;
        zombieRenderer.material.color = skinColor;
    }

    private void Start()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        StartCoroutine(UpdatePath());
    }

    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        zombieAnimator.SetBool("HasTarget", hasTarget);
    }

    IEnumerator UpdatePath()
    {
        while (!dead)
        {
            LivingEntity nearerTarget = FindNearerTarget();

            if (nearerTarget != null)
            {
                targetEntity = nearerTarget;
            }

            if (hasTarget)
            {
                navMeshAgent.isStopped = false;
                navMeshAgent.SetDestination(targetEntity.transform.position);
            }
            else
            {
                navMeshAgent.isStopped = true;
            }

            yield return new WaitForSeconds(0.25f);
        }
    }

    LivingEntity FindNearerTarget()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, 20f, whatIsTarget);

        LivingEntity nearestTarget = null;
        float nearestDistance = hasTarget
            ? Vector3.Distance(transform.position, targetEntity.transform.position)
            : Mathf.Infinity;

        for (int i = 0; i < colliders.Length; i++)
        {
            LivingEntity livingEntity = colliders[i].GetComponent<LivingEntity>();

            if (livingEntity == null || livingEntity.dead)
                continue;

            float distance = Vector3.Distance(transform.position, livingEntity.transform.position);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestTarget = livingEntity;
            }
        }

        return nearestTarget;
    }

    [PunRPC]
    public override void OnDamage(float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (!dead)
        {
            hitEffect.transform.position = hitPoint;
            hitEffect.transform.rotation = Quaternion.LookRotation(hitNormal);
            hitEffect.Play();

            zombieAudioPlayer.PlayOneShot(hitSound);
        }

        base.OnDamage(damage, hitPoint, hitNormal);
    }

    public override void Die()
    {
        base.Die();

        Collider[] zombieColliders = GetComponents<Collider>(); // 캡슐콜라이더(몸), 박스콜라이더(공격범위)
        for(int i = 0; i< zombieColliders.Length; i++)
        {
            zombieColliders[i].enabled = false;
        }

        // AI 추적을 중지하고 내비메시 컴포넌트 비활성화
        navMeshAgent.isStopped = true;
        navMeshAgent.enabled = false;

        zombieAnimator.SetTrigger("Die");
        zombieAudioPlayer.PlayOneShot(deathSound);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        // 자신이 사망하지 않았으며,
        // 최근 공격 시점에서 timeBetAttack 이상 시간이 지났다면 공격 가능
        if (!dead && Time.time >= lastAttackTime + timeBetAttack)
        {
            // 상대방의 LivingEntity 타입 가져오기 시도
            LivingEntity attackTarget = other.GetComponent<LivingEntity>();

            // 상대방의 LivingEntity가 자신의 추적 대상이라면 공격 실행
            if(attackTarget!= null && attackTarget == targetEntity)
            {
                // 최근 공격 시간 갱신
                lastAttackTime = Time.time;

                // 상대방의 피격 위치와 피격 방향을 근삿값으로 계산
                Vector3 hitPoint = other.ClosestPoint(transform.position);
                Vector3 hitNormal = transform.position - other.transform.position;

                attackTarget.OnDamage(damage, hitPoint, hitNormal);
            }
        }

        // 트리거 충돌한 상대방 게임 오브젝트가 추적 대상이라면 공격 실행
    }
}
