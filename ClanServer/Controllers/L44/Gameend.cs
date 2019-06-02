﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;

using eAmuseCore.KBinXML;

using ClanServer.Helpers;
using ClanServer.Routing;
using ClanServer.Models;

namespace ClanServer.Controllers.L44
{
    [ApiController, Route("L44")]
    public class GameendController : ControllerBase
    {
        private readonly ClanServerContext ctx;

        public GameendController(ClanServerContext ctx)
        {
            this.ctx = ctx;
        }

        [HttpPost, Route("8"), XrpcCall("gameend.regist")]
        public async Task<ActionResult<EamuseXrpcData>> Register([FromBody] EamuseXrpcData xrpcData)
        {
            try
            {
                XElement dataE = xrpcData.Document.Element("call").Element("gameend").Element("data");
                XElement playerE = dataE.Element("player");

                byte[] refId = playerE.Element("refid").Value.ToBytesFromHex();

                Card card = await ctx.FindCardAsync(c => c.RefId.SequenceEqual(refId));
                if (card == null)
                    return NotFound();

                var player = card.Player;

                await ctx.Entry(player).Reference(p => p.JubeatProfile).LoadAsync();

                var profile = player.JubeatProfile;

                await ctx.Entry(profile).Reference(p => p.ClanData).LoadAsync();
                await ctx.Entry(profile).Reference(p => p.ClanSettings).LoadAsync();

                if (profile.ClanData == null)
                    profile.ClanData = new JubeatClanProfileData();
                if (profile.ClanSettings == null)
                    profile.ClanSettings = new JubeatClanSettings();

                var data = profile.ClanData;
                var settings = profile.ClanSettings;

                XElement teamE = playerE.Element("team");
                data.Team = byte.Parse(teamE.Attribute("id").Value);
                data.Street = int.Parse(teamE.Element("street").Value);
                data.Section = int.Parse(teamE.Element("section").Value);
                data.HouseNo1 = short.Parse(teamE.Element("house_number_1").Value);
                data.HouseNo1 = short.Parse(teamE.Element("house_number_2").Value);

                XElement infoE = dataE.Element("info");
                data.PlayTime = int.Parse(infoE.Element("play_time").Value);

                XElement pInfoE = playerE.Element("info");
                data.TuneCount = int.Parse(pInfoE.Element("tune_cnt").Value);
                data.ClearCount = int.Parse(pInfoE.Element("clear_cnt").Value);
                data.FcCount = int.Parse(pInfoE.Element("fc_cnt").Value);
                data.ExCount = int.Parse(pInfoE.Element("ex_cnt").Value);
                data.MatchCount = int.Parse(pInfoE.Element("match_cnt").Value);
                data.BeatCount = int.Parse(pInfoE.Element("beat_cnt").Value);
                data.SaveCount = int.Parse(pInfoE.Element("save_cnt").Value);
                data.SavedCount = int.Parse(pInfoE.Element("saved_cnt").Value);
                data.BonusTunePoints = int.Parse(pInfoE.Element("bonus_tune_points").Value);
                data.BonusTunePlayed = pInfoE.Element("is_bonus_tune_played").Value == "1";

                XElement lastE = playerE.Element("last");
                settings.ExpertOption = sbyte.Parse(lastE.Element("expert_option").Value);
                settings.Sort = sbyte.Parse(lastE.Element("sort").Value);
                settings.Category = sbyte.Parse(lastE.Element("category").Value);

                XElement settingsE = lastE.Element("settings");
                settings.Marker = sbyte.Parse(settingsE.Element("marker").Value);
                settings.Theme = sbyte.Parse(settingsE.Element("theme").Value);
                settings.RankSort = sbyte.Parse(settingsE.Element("rank_sort").Value);
                settings.ComboDisplay = sbyte.Parse(settingsE.Element("combo_disp").Value);
                settings.Matching = sbyte.Parse(settingsE.Element("matching").Value);
                settings.Hard = sbyte.Parse(settingsE.Element("hard").Value);
                settings.Hazard = sbyte.Parse(settingsE.Element("hazard").Value);

                IEnumerable<XElement> tunes = dataE.Element("result").Elements("tune");

                foreach (XElement tune in tunes)
                {
                    XElement tunePlayer = tune.Element("player");
                    XElement tuneScore = tunePlayer.Element("score");

                    int musicId = int.Parse(tune.Element("music").Value);
                    sbyte seq = sbyte.Parse(tuneScore.Attribute("seq").Value);

                    JubeatScore score = ctx.JubeatScores
                        .Where(s => s.MusicID == musicId && s.Seq == seq && s.ProfileID == profile.ID)
                        .SingleOrDefault();

                    if (score == null)
                    {
                        score = new JubeatScore()
                        {
                            ProfileID = profile.ID,
                            MusicID = musicId,
                            Seq = seq
                        };

                        ctx.JubeatScores.Add(score);
                    }

                    score.Timestamp = long.Parse(tune.Element("timestamp").Value);
                    score.Score = int.Parse(tunePlayer.Element("score").Value);
                    score.Clear = sbyte.Parse(tuneScore.Attribute("clear").Value);
                    score.IsHardMode = tunePlayer.Element("is_hard_mode").Value == "1";
                    score.IsHazardMode = tunePlayer.Element("is_hazard_end").Value == "1";
                    score.NumPerfect = short.Parse(tunePlayer.Element("nr_perfect").Value);
                    score.NumGreat = short.Parse(tunePlayer.Element("nr_great").Value);
                    score.NumGood = short.Parse(tunePlayer.Element("nr_good").Value);
                    score.NumPoor = short.Parse(tunePlayer.Element("nr_poor").Value);
                    score.NumMiss = short.Parse(tunePlayer.Element("nr_miss").Value);
                    score.BestScore = int.Parse(tunePlayer.Element("best_score").Value);
                    score.BestClear = int.Parse(tunePlayer.Element("best_clear").Value);
                    score.PlayCount = int.Parse(tunePlayer.Element("play_cnt").Value);
                    score.ClearCount = int.Parse(tunePlayer.Element("clear_cnt").Value);
                    score.FcCount = int.Parse(tunePlayer.Element("fc_cnt").Value);
                    score.ExcCount = int.Parse(tunePlayer.Element("ex_cnt").Value);

                    string[] mbarStrs = tunePlayer.Element("mbar").Value.Split(' ');
                    score.MBar = Array.ConvertAll(mbarStrs, s => byte.Parse(s));
                }

                //TODO: save jubility, player->jubility

                await ctx.SaveChangesAsync();

                xrpcData.Document = new XDocument(new XElement("response", new XElement("gameend")));
                return xrpcData;
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
                return StatusCode(500);
            }
        }

        [HttpPost, Route("8"), XrpcCall("gameend.final")]
        public ActionResult<EamuseXrpcData> Final([FromBody] EamuseXrpcData data)
        {
            data.Document = new XDocument(new XElement("response", new XElement("gameend")));
            return data;
        }
    }
}
