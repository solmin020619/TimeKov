using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(EnemyData))]
public class EnemyDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("기본 설정 (Basic Info)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("enemyName"));

        SerializedProperty typeProp = serializedObject.FindProperty("enemyType");
        EditorGUILayout.PropertyField(typeProp);

        EnemyType currentType = (EnemyType)typeProp.enumValueIndex;

        EditorGUILayout.Space(5);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxHP"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("moveSpeed"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("chaseSpeed"));

        EditorGUILayout.PropertyField(serializedObject.FindProperty("patrolRadius"));

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("감지 설정 (Detection)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("visionRange"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("visionAngle"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("proximityRange"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("giveUpChaseRange"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("provokedDuration"));

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("공격 설정 (Combat)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("attackDamage"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("attackRange"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("attackCooldown"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("attackHitDelay"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("attackAnimLength"));


        EditorGUILayout.Space(15);

        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        if (currentType == EnemyType.Melee)
        {
            // === 근접(Melee) 타입일 때만 보이는 부분 ===
            EditorGUILayout.LabelField(" [ 근접 적 전용 설정 ]", EditorStyles.boldLabel);
            GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.PropertyField(serializedObject.FindProperty("useJumpAttack"));

            if (serializedObject.FindProperty("useJumpAttack").boolValue)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("jumpAttackDamage"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("jumpAttackRadius"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("jumpWindup"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("jumpHitDelay"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("jumpFullTime"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("jumpLungeSpeed"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("jumpChanceOnMiss"));
            }

            EditorGUILayout.EndVertical();
            GUI.backgroundColor = Color.white;
        }
        else if (currentType == EnemyType.SuicideBomber)
        {
            EditorGUILayout.LabelField(" [ 자폭 적 전용 설정 ]", EditorStyles.boldLabel);
            GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.PropertyField(serializedObject.FindProperty("explosionRadius"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("dieAfterAttack"));

            EditorGUILayout.EndVertical();
            GUI.backgroundColor = Color.white;
        }
        else if (currentType == EnemyType.Gun)
        {
            EditorGUILayout.LabelField("원거리 적 설정은 아직 구현되지 않았습니다.");
        }

        serializedObject.ApplyModifiedProperties();
    }
}