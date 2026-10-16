import { Injectable } from "@angular/core";
import { ApiService } from "./api.service";
import { LocationResponse } from "../models/locationResponse";
import { IntelResult } from "../models/intelResult";
import { environment } from "src/environments/environment";

@Injectable({ providedIn: 'root' })
export class IntelService {
  constructor(private api: ApiService) {}

 

  getIntel() {
  return this.api.get<IntelResult>(environment.apiUrl +'/intel');
}
}