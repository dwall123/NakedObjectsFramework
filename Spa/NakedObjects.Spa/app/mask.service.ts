﻿import * as _ from "lodash";
import * as Ro from './nakedobjects.rointerfaces';
import { DatePipe, CurrencyPipe, DecimalPipe } from '@angular/common';
import { MaskServiceConfig } from './mask.service.config';
import { Injectable } from '@angular/core';

export interface ILocalFilter {
    filter(val: any): string;
}

export interface IMaskMap {
    [index: string]: { [index: string]: ILocalFilter };
}

class LocalStringFilter implements ILocalFilter {

    filter(val: any): string {
        return val ? val.toString() : "";
    }
}

class LocalCurrencyFilter implements ILocalFilter {

    constructor(private symbol?: string, private fractionSize?: number) {}

    filter(val: any): string {
        const pipe = new CurrencyPipe();
        // return $filter("currency")(val, this.symbol, this.fractionSize);
        return pipe.transform(val);
    }
}

class LocalDateFilter implements ILocalFilter {

    constructor(private mask?: string, private tz?: string) {}

    filter(val: any): string {
        const pipe = new DatePipe();
        //   return $filter("date")(val, this.mask, this.tz);
        return pipe.transform(val, this.mask);
    }
}

class LocalNumberFilter implements ILocalFilter {

    constructor(private fractionSize?: number) {}

    filter(val: any): string {
        //  return $filter("number")(val, this.fractionSize);
     
        const pipe = new DecimalPipe();
        // return $filter("currency")(val, this.symbol, this.fractionSize);
        return pipe.transform(val);
    }
}

@Injectable()
export class Mask {

    private maskMap: IMaskMap = {
        string: {},
        "date-time": {},
        date: {},
        time: {},
        "utc-millisec": {},
        "big-integer": {},
        "big-decimal": {},
        blob: {},
        clob: {},
        decimal: {},
        int: {}
    };

    constructor(private config: MaskServiceConfig) {
        config.configure(this);
    }



    defaultLocalFilter(format: Ro.formatType): ILocalFilter {
        switch (format) {
        case ("string"):
            return new LocalStringFilter();
        case ("date-time"):
            return new LocalDateFilter("d MMM yyyy HH:mm:ss");
        case ("date"):
            return new LocalDateFilter("d MMM yyyy", "+0000");
        case ("time"):
            return new LocalDateFilter("HH:mm", "+0000");
        case ("utc-millisec"):
            return new LocalNumberFilter();
        case ("big-integer"):
            return new LocalNumberFilter();
        case ("big-decimal"):
            return new LocalNumberFilter();
        case ("blob"):
            return new LocalStringFilter();
        case ("clob"):
            return new LocalStringFilter();
        case ("decimal"):
            return new LocalNumberFilter();
        case ("int"):
            return new LocalNumberFilter();
        default:
            return new LocalStringFilter();
        }
    };

    private customFilter(format: Ro.formatType, remoteMask: string) {
        if (this.maskMap[format as string] && remoteMask) {
            return this.maskMap[format as string][remoteMask];
        }
        return undefined;
    }

    toLocalFilter(remoteMask: string, format: Ro.formatType) {
        return this.customFilter(format, remoteMask) || this.defaultLocalFilter(format);
    };

    setNumberMaskMapping(customMask: string, format: Ro.formatType, fractionSize?: number) {
        this.maskMap[format as string][customMask] = new LocalNumberFilter(fractionSize);
    };

    setDateMaskMapping(customMask: string, format: Ro.formatType, mask: string, tz?: string) {
        this.maskMap[format as string][customMask] = new LocalDateFilter(mask, tz);
    };

    setCurrencyMaskMapping(customMask: string, format: Ro.formatType, symbol?: string, fractionSize?: number) {
        this.maskMap[format as string][customMask] = new LocalCurrencyFilter(symbol, fractionSize);
    };
}