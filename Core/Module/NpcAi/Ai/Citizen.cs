using System.Threading.Tasks;
using Helpers;

namespace Core.Module.NpcAi.Ai;

public class Citizen : DefaultNpc
{
    public virtual string FnTradeSkill0 { get; set; } = "";
    public virtual string FnTradeSkill1 { get; set; } = "";
    public virtual string FnTradeSkill2 { get; set; } = "";
    public virtual string FnHi { get; set; } = "chi.htm";
    public virtual string FnFeudInfo { get; set; } = "defaultfeudinfo.htm";
    public virtual string FnNoFeudInfo { get; set; } = "nofeudinfo.htm";
    public virtual string FnYouAreChaotic { get; set; } = "wyac.htm";
    public virtual string FnBracketL { get; set; } = "[";
    public virtual string FnBracketR { get; set; } = "]";
    public override int MoveAroundSocial { get; set; }
    public override int MoveAroundSocial1 { get; set; }
    public override int MoveAroundSocial2 { get; set; }
    public virtual int HavePet { get; set; } = 0;
    public virtual int Silhouette { get; set; } = 1000130;
    public virtual int FriendShip1 { get; set; } = 0;
    public virtual int FriendShip2 { get; set; } = 0;
    public virtual int FriendShip3 { get; set; } = 0;
    public virtual int FriendShip4 { get; set; } = 0;
    public virtual int FriendShip5 { get; set; } = 0;
    public virtual int NoFnHi { get; set; }
    public virtual string FnNoFriend { get; set; }
        
    public override void Created()
    {
        if (MoveAroundSocial > 0 || MoveAroundSocial1 > 0)
        {
            MySelf.AddTimerEx(1671, 10000);
        }
    }

    public override async Task Talked(Talker talker)
    {
        if (talker.Karma > 0)
        {
            await MySelf.ShowPage(talker, FnYouAreChaotic);
            return;
        }
        if (NoFnHi == 1)
        {
            return;
        }
        if (FriendShip1 == 0)
        {
            await MySelf.ShowPage(talker, FnHi);
        }
        else if (MySelf.OwnItemCount(talker, FriendShip1) > 0 || MySelf.OwnItemCount(talker, FriendShip2) > 0 ||
                 MySelf.OwnItemCount(talker, FriendShip3) > 0 || MySelf.OwnItemCount(talker, FriendShip4) > 0 ||
                 MySelf.OwnItemCount(talker, FriendShip5) > 0)
        {
            await MySelf.ShowPage(talker, FnHi);
        }
        else
        {
            await MySelf.ShowPage(talker, FnNoFriend);
        }
    }

    public override async Task TimerFiredEx(int timerId)
    {
        if (MoveAroundSocial > 0 && Rnd.Next(100) < 40)
        {
            await MySelf.AddEffectActionDesire(MySelf.Sm, 3, ((MoveAroundSocial * 1000) / 30), 50);
        }
        else if (MoveAroundSocial1 > 0 && Rnd.Next(100) < 40)
        {
            await MySelf.AddEffectActionDesire(MySelf.Sm, 2, ((MoveAroundSocial1 * 1000) / 30), 50);
        }
        MySelf.AddTimerEx(1671, 10000);
    }

    public override Task MenuSelected(Talker talker, int ask, int reply, string fhtml0)
    {
        if (ask == -1000)
        {
                
        }

        if (ask == -303)
        {
                
        }

        return Task.FromResult(1);
    }
}