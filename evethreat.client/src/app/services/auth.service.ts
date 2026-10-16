import { Injectable } from "@angular/core";
import { ApiService } from "./api.service";
import { environment } from "src/environments/environment";

@Injectable({ providedIn: 'root' })
export class AuthService {
  constructor(private api: ApiService) {}

  login() {
    window.location.href = `${environment.apiUrl}/login`;
  }
}