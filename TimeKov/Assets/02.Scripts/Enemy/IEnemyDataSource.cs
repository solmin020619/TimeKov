// 적 데이터(SO) 제공자.
// 일반몹은 EnemyBrain(BT 사용)이, 보스는 전용 컨트롤러(BT 미사용)가 구현한다.
// EnemyHealth 같은 공유 컴포넌트가 EnemyBrain 이라는 특정 구현에 묶이지 않게 하는 것이 목적.
// 이게 없으면 보스는 EnemyBrain 이 없어서 킬 이벤트가 "unknown" 으로 올라가고
// 도감/드롭/퀘스트가 전부 그 몬스터를 못 찾는다.
public interface IEnemyDataSource
{
    MeleeEnemyData Data { get; }
}
