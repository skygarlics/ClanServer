﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using eAmuseCore.KBinXML;

using ClanServer.Routing;
using ClanServer.Helpers;
using ClanServer.Models;

namespace ClanServer.Controllers.L44
{
    [ApiController, Route("L44")]
    public class GametopController : ControllerBase
    {
        private readonly ClanServerContext ctx;

        public GametopController(ClanServerContext ctx)
        {
            this.ctx = ctx;
        }

        [HttpPost, Route("8"), XrpcCall("gametop.regist")]
        public async Task<ActionResult<EamuseXrpcData>> Register([FromBody] EamuseXrpcData data)
        {
            XElement gametop = data.Document.Element("call").Element("gametop");
            XElement player = gametop.Element("data").Element("player");
            byte[] refId = player.Element("refid").Value.ToBytesFromHex();
            string name = player.Element("name").Value;

            Card card = await ctx.FindCardAsync(c => c.RefId.SequenceEqual(refId));

            if (card == null || card.Player == null)
                return NotFound();

            await ctx.Entry(card.Player).Reference(p => p.JubeatProfile).LoadAsync();

            if (card.Player.JubeatProfile == null)
                card.Player.JubeatProfile = new JubeatProfile();

            card.Player.JubeatProfile.JID = Math.Abs(new Random().Next());
            card.Player.JubeatProfile.Name = name;

            await ctx.SaveChangesAsync();

            data.Document = new XDocument(new XElement("response", new XElement("gametop",
                new XElement("data",
                    GetInfoElement(),
                    await GetPlayerElement(card)
                )
            )));

            return data;
        }

        [HttpPost, Route("8"), XrpcCall("gametop.get_pdata")]
        public async Task<ActionResult<EamuseXrpcData>> GetPdata([FromBody] EamuseXrpcData data)
        {
            var gametop = data.Document.Element("call").Element("gametop");
            var player = gametop.Element("data").Element("player");

            byte[] refId = player.Element("refid").Value.ToBytesFromHex();

            Card card = await ctx.FindCardAsync(c => c.RefId.SequenceEqual(refId));

            if (card == null || card.Player == null)
                return NotFound();

            await ctx.Entry(card.Player).Reference(p => p.JubeatProfile).LoadAsync();

            if (card.Player.JubeatProfile == null)
                return NotFound();

            data.Document = new XDocument(new XElement("response", new XElement("gametop",
                new XElement("data",
                    GetInfoElement(),
                    await GetPlayerElement(card)
                )
            )));

            return data;
        }

        public static XElement GetInfoElement()
        {
            return new XElement("info",
                new XElement("event_info",
                    new XElement("event", new XAttribute("type", "15"),
                        new KU8("state", 1)
                    ),
                    new XElement("event", new XAttribute("type", "5"),
                        new KU8("state", 0)
                    ),
                    new XElement("event", new XAttribute("type", "6"),
                        new KU8("state", 0)
                    )
                ),
                new XElement("share_music"),
                new XElement("genre_def_music"),
                new KS32("black_jacket_list", 64, 0),
                new KS32("white_music_list", 64, -1),
                new KS32("white_marker_list", 16, -1),
                new KS32("white_theme_list", 16, -1),
                new KS32("open_music_list", 64, -1),
                new KS32("shareable_music_list", 64, -1),
                new XElement("jbox",
                    new KS32("point", 1),
                    new XElement("emblem",
                        new XElement("normal",
                            new KS16("index", 50)
                        ),
                        new XElement("premium",
                            new KS16("index", 50)
                        )
                    )
                ),
                new XElement("collection",
                    new XElement("rating_s")
                ),
                new XElement("expert_option",
                    new KBool("is_available", true)
                ),
                new XElement("all_music_matching",
                    new KBool("is_available", false)
                ),
                new XElement("department",
                    new XElement("pack_list")
                )
            );
        }

        private async Task<XElement> GetPlayerElement(Card card)
        {
            var player = card.Player;
            var profile = player.JubeatProfile;

            bool changed = false;

            await ctx.Entry(profile).Reference(p => p.ClanData).LoadAsync();
            await ctx.Entry(profile).Reference(p => p.ClanSettings).LoadAsync();

            if (profile.ClanData == null)
            {
                Random rng = new Random();

                profile.ClanData = new JubeatClanProfileData()
                {
                    Team = (byte)(rng.Next() % 4 + 1),
                    Street = rng.Next() % 120,
                    Section = rng.Next() % 120,
                    HouseNo1 = (short)(rng.Next() % 250),
                    HouseNo2 = (short)(rng.Next() % 250),

                    TuneCount = 0,
                    ClearCount = 0,
                    FcCount = 0,
                    ExCount = 0,
                    MatchCount = 0,
                    BeatCount = 0,
                    SaveCount = 0,
                    SavedCount = 0,
                    BonusTunePoints = 0,
                    BonusTunePlayed = false,
                };

                changed = true;
            }

            if (profile.ClanSettings == null)
            {
                profile.ClanSettings = new JubeatClanSettings()
                {
                    Sort = 0,
                    Category = 0,
                    Marker = 3,
                    Theme = 0,
                    RankSort = 0,
                    ComboDisplay = 1,
                    Hard = 0,
                    Hazard = 0,

                    Title = 0,
                    Parts = 0,

                    EmblemBackground = 0,
                    EmblemMain = 823,
                    EmblemOrnament = 0,
                    EmblemEffect = 0,
                    EmblemBalloon = 0
                };

                changed = true;
            }

            if (changed)
                await ctx.SaveChangesAsync();

            var settings = profile.ClanSettings;
            var data = profile.ClanData;

            var latestScore = await ctx.JubeatScores
                .Where(s => s.ProfileID == profile.ID)
                .OrderByDescending(s => s.Timestamp)
                .FirstOrDefaultAsync();

            if (latestScore == null)
                latestScore = new JubeatScore();

            return new XElement("player",
                new KS32("jid", profile.JID),
                new KS32("session_id", 1),
                new KStr("name", profile.Name),
                new KU64("event_flag", 2),
                new XElement("info",
                    new KS32("tune_cnt", data.TuneCount),
                    new KS32("save_cnt", data.SaveCount),
                    new KS32("saved_cnt", data.SavedCount),
                    new KS32("fc_cnt", data.FcCount),
                    new KS32("ex_cnt", data.ExCount),
                    new KS32("clear_cnt", data.ClearCount),
                    new KS32("match_cnt", data.MatchCount),
                    new KS32("beat_cnt", data.BeatCount),
                    new KS32("mynews_cnt", 0),
                    new KS32("mtg_entry_cnt", 0),
                    new KS32("mtg_hold_cnt", 0),
                    new KU8("mtg_result", 0),
                    new KS32("bonus_tune_points", data.BonusTunePoints),
                    new KBool("is_bonus_tune_played", data.BonusTunePlayed)
                ),
                new XElement("last",
                    new KS64("play_time", data.PlayTime),
                    new KStr("shopname", "xxx"),
                    new KStr("areaname", "xxx"),
                    new KS32("music_id", latestScore.MusicID),
                    new KS8("seq_id", latestScore.Seq),
                    new KS8("sort", settings.Sort),
                    new KS8("category", settings.Category),
                    new KS8("expert_option", settings.ExpertOption),
                    new XElement("settings",
                        new KS8("marker", settings.Marker),
                        new KS8("theme", settings.Theme),
                        new KS16("title", settings.Title),
                        new KS16("parts", settings.Parts),
                        new KS8("rank_sort", settings.RankSort),
                        new KS8("combo_disp", settings.ComboDisplay),
                        new KS16("emblem", new short[]
                        {
                            settings.EmblemBackground,
                            settings.EmblemMain,
                            settings.EmblemOrnament,
                            settings.EmblemEffect,
                            settings.EmblemBalloon
                        }),
                        new KS8("matching", settings.Matching),
                        new KS8("hard", settings.Hard),
                        new KS8("hazard", settings.Hazard)
                    )
                ),
                new XElement("item",
                    new KS32("music_list", 64, -1),
                    new KS32("secret_list", 64, -1),
                    new KS32("theme_list", 16, -1),
                    new KS32("marker_list", 16, -1),
                    new KS32("title_list", 160, -1),
                    new KS32("parts_list", 160, -1),
                    new KS32("emblem_list", 96, -1),
                    new KS32("commu_list", 16, -1),
                    new XElement("new",
                        new KS32("secret_list", 64, 0),
                        new KS32("theme_list", 16, 0),
                        new KS32("marker_list", 16, 0)
                    )
                ),
                new XElement("team", new XAttribute("id", data.Team),
                    new KS32("section", data.Section),
                    new KS32("street", data.Street),
                    new KS32("house_number_1", data.HouseNo1),
                    new KS32("house_number_2", data.HouseNo2),
                    new XElement("move",
                        new XAttribute("house_number_1", data.HouseNo1),
                        new XAttribute("house_number_2", data.HouseNo2),
                        new XAttribute("id", data.Team),
                        new XAttribute("section", data.Section),
                        new XAttribute("street", data.Street)
                    )
                ),
                new XElement("jbox",
                    new KS32("point", 700),
                    new XElement("emblem",
                        new XElement("normal",
                            new KS16("index", 1182)
                        ),
                        new XElement("premium",
                            new KS16("index", 1197)
                        )
                    )
                ),
                new XElement("news",
                    new KS16("checked", 0),
                    new KU32("checked_flag", 1)
                ),
                new XElement("free_first_play",
                    new KBool("is_available", false)
                ),
                new XElement("event_info",
                    new XElement("event", new XAttribute("type", "15"),
                        new KU8("state", 1)
                    )
                ),
                new XElement("new_music"),
                new XElement("gift_list"),
                new XElement("jubility", new XAttribute("param", "0"),
                    new XElement("target_music_list")
                ),
                new XElement("born",
                    new KS8("status", 1),
                    new KS16("year", 2000)
                ),
                new XElement("question_list"),
                new XElement("official_news",
                    new XElement("news_list")
                ),
                GenDroplist(),
                new XElement("daily_bonus_list"),
                GenClanCourseList(),
                new XElement("server"),
                new XElement("rivallist"),
                new XElement("fc_challenge",
                    new XElement("today",
                        new KS32("music_id", 40000057),
                        new KU8("state", 0)
                    ),
                    new XElement("whim",
                        new KS32("music_id", 30000041),
                        new KU8("state", 80)
                    )
                ),
                new XElement("navi",
                    new KU64("flag", 122)
                )
            );
        }

        private static XElement GenClanCourseList()
        {
            XElement categoryList = new XElement("category_list");

            for (int i = 1; i <= 6; ++i)
            {
                categoryList.Add(new XElement("category", new XAttribute("id", i),
                    new KBool("is_display", true)
                ));
            }

            return new XElement("clan_course_list",
                new XElement("clan_course", new XAttribute("id", "50"),
                    new KS8("status", 1)
                ),
                categoryList
            );
        }

        private static XElement GenDroplist()
        {
            var res = new XElement("drop_list");
            var item_list = new XElement("item_list");

            for (int i = 1; i <= 10; ++i)
            {
                item_list.Add(new XElement("item", new XAttribute("id", i),
                    new KS32("num", 0)
                ));
            }

            for (int i = 1; i <= 10; ++i)
            {
                res.Add(new XElement("drop", new XAttribute("id", i),
                    new KS32("exp", 0),
                    new KS32("flag", 0),
                    item_list
                ));
            }

            return res;
        }

        [HttpPost, Route("8"), XrpcCall("gametop.get_mdata")]
        public async Task<ActionResult<EamuseXrpcData>> GetMdata([FromBody] EamuseXrpcData data)
        {
            var gametop = data.Document.Element("call").Element("gametop");
            var player = gametop.Element("data").Element("player");
            int jid = int.Parse(player.Element("jid").Value);

            var profile = await ctx.JubeatProfiles.SingleOrDefaultAsync(p => p.JID == jid);
            if (profile == null)
                return NotFound();

            var scores = ctx.JubeatScores
                .Where(s => s.ProfileID == profile.ID)
                .GroupBy(s => s.MusicID, (s, l) => new
                {
                    l.First().MusicID,
                    Seqs = l.Select(v => v.Seq).ToArray(),
                    Scores = l.Select(v => v.Score).ToArray(),
                    Clears = l.Select(v => v.Clear).ToArray(),
                    PlayCounts = l.Select(v => v.PlayCount).ToArray(),
                    ClearCounts = l.Select(v => v.ClearCount).ToArray(),
                    FcCounts =  l.Select(v => v.FcCount).ToArray(),
                    ExCounts = l.Select(v => v.ExcCount).ToArray(),
                    Bars = l.Select(v => v.MBar).ToArray(),
                });

            XElement mdataList = new XElement("mdata_list");

            foreach (var score in scores)
            {
                var scoreRes = new int[3];
                var clearRes = new sbyte[3];
                var playCnt = new int[3];
                var clearCnt = new int[3];
                var fcCnt = new int[3];
                var exCnt = new int[3];
                var bars = new[] { new byte[30], new byte[30], new byte[30] };

                for (sbyte seq = 0; seq < 3; ++seq)
                {
                    int seqIdx = Array.IndexOf(score.Seqs, seq);
                    if (seqIdx < 0)
                        continue;

                    scoreRes[seq] = score.Scores[seqIdx];
                    clearRes[seq] = score.Clears[seqIdx];
                    playCnt[seq] = score.PlayCounts[seqIdx];
                    clearCnt[seq] = score.ClearCounts[seqIdx];
                    fcCnt[seq] = score.FcCounts[seqIdx];
                    exCnt[seq] = score.ExCounts[seqIdx];
                    bars[seq] = score.Bars[seqIdx];
                }

                mdataList.Add(new XElement("music", new XAttribute("music_id", score.MusicID),
                    new KS32("score", scoreRes),
                    new KS8("clear", clearRes),
                    new KS32("play_cnt", playCnt),
                    new KS32("clear_cnt", clearCnt),
                    new KS32("fc_cnt", fcCnt),
                    new KS32("ex_cnt", exCnt),
                    new KU8("bar", bars[2]).AddAttr("seq", 2),
                    new KU8("bar", bars[0]).AddAttr("seq", 0),
                    new KU8("bar", bars[1]).AddAttr("seq", 1)
                ));
            }
            
            data.Document = new XDocument(new XElement("response", new XElement("gametop", new XElement("data",
                new XElement("player",
                    new KS32("jid", jid),
                    mdataList
                )
            ))));

            return data;
        }

        [HttpPost, Route("8"), XrpcCall("gametop.get_meeting")]
        public ActionResult<EamuseXrpcData> GetMeeting([FromBody] EamuseXrpcData data)
        {
            var gametop = data.Document.Element("call").Element("gametop");
            var player = gametop.Element("data").Element("player");
            _ = int.Parse(player.Element("jid").Value);

            data.Document = new XDocument(new XElement("response", new XElement("gametop", new XElement("data",
                new XElement("meeting",
                    new XElement("single", new XAttribute("count", 0),
                        new XElement("info")
                    )
                ),
                new XElement("reward",
                    new KS32("total", 0),
                    new KS32("point", 0)
                )
            ))));

            return data;
        }
    }
}
