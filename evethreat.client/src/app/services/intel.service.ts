import { Injectable } from "@angular/core";
import { ApiService } from "./api.service";
import { LocationResponse } from "../models/locationResponse";
import { IntelResult } from "../models/intelResult";

@Injectable({ providedIn: 'root' })
export class IntelService {
  constructor(private api: ApiService) {}

 

  getIntel() {
  return this.api.get<IntelResult>('intel');
}
}