﻿using Starward.Core;
using System;

namespace Starward.Models;

public class GachaLogUrl
{

    public GachaLogUrl() { }


    public GachaLogUrl(GameBiz gameBiz, int uid, string url)
    {
        GameBiz = gameBiz;
        Uid = uid;
        Url = url;
        Time = DateTime.Now;
    }


    public GameBiz GameBiz { get; set; }


    public int Uid { get; set; }


    public string Url { get; set; }


    public DateTime Time { get; set; }


}
