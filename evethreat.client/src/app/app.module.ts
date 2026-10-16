import { HttpClientModule } from '@angular/common/http';
import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';

import { AppComponent } from './app.component';
import { LoginComponent } from './components/login.component';
import { DataComponent } from './components/data.component';
import { AppRoutingModule } from './app.routing.module';
import { RouterModule } from '@angular/router';

@NgModule({
  declarations: [
    AppComponent,
    LoginComponent,
    DataComponent
  ],
  imports: [
    BrowserModule, HttpClientModule,  AppRoutingModule,   // loads your routes
    RouterModule        // makes <router-outlet> valid
  ],
  providers: [],
  bootstrap: [AppComponent]
})
export class AppModule { }
