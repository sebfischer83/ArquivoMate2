import { ChangeDetectionStrategy, Component, inject, ViewChild } from '@angular/core';
import { FilePondModule, registerPlugin } from "ngx-filepond";
import { FilePondComponent } from "ngx-filepond";
import { FilePondOptions } from "filepond";
import { OAuthService } from 'angular-oauth2-oidc';


@Component({
  standalone: true,
  selector: 'app-dashboard',
  imports: [FilePondModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardComponent {
  @ViewChild("myPond") myPond!: FilePondComponent;
  auth = inject(OAuthService);

 pondOptions: FilePondOptions = {
    allowMultiple: true,
    labelIdle: "Drop files here...",
    name: "file",
    server: {
      process: {
        url: 'https://localhost:5000/api/documents',
        method: 'POST',
        headers: {
          'X-Requested-With': 'XMLHttpRequest',
          'Authorization': `Bearer ${this.auth.getAccessToken()}`
        },
        withCredentials: false,
        ondata: (formData) => {
          console.log("Preparing data for upload", formData);
          formData.append('Language', 'deu');
          return formData;
        },

        onload: (response) => {
          console.log("File uploaded successfully", response);
          
          return '';
        },
        onerror: (response) => {
          console.error("File upload failed", response);
          return response;
        }
      }      
    },
  };

   pondHandleInit() {
    console.log("FilePond has initialised", this.myPond);
  }

  pondHandleAddFile(event: any) {
    console.log("A file was added", event);
  }

  pondHandleActivateFile(event: any) {
    console.log("A file was activated", event);
  }

  pondHandleProcessFile(event: any) {
    // Entferne die Datei nach erfolgreichem Upload
    if (event && event.file && this.myPond && this.myPond['pond']) {
      this.myPond['pond'].removeFile(event.file.id);
    }
  }
 }
