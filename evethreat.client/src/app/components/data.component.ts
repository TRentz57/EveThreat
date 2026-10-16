import { Component, OnInit } from '@angular/core';
import { IntelService } from '../services/intel.service';
import { IntelResult } from '../models/intelResult';

@Component({
  selector: 'app-data',
  templateUrl: './data.component.html'
})
export class DataComponent implements OnInit {

  loading = true;
  error: string | null = null;
  intel: any = null;
  intelResult!: IntelResult

  constructor(private intelService: IntelService) {}

  ngOnInit() {
    
     this.loadIntel();

 

  
   
  }
  loadIntel(){
     this.intelService.getIntel().subscribe({
      next: (data) => {
        console.log(data)
        this.intelResult = data;
        this.loading = false;
        
      },
      error: () => {
        this.error = 'Failed to load intel';
        this.loading = false;
      }
    });
  }
}