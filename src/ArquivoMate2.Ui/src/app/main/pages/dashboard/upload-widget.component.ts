import { ChangeDetectionStrategy, Component, ViewChild, inject } from '@angular/core';
import { FilePondModule, FilePondComponent } from 'ngx-filepond';
import { FilePondOptions } from 'filepond';
import { OAuthService } from 'angular-oauth2-oidc';
import { ApiConfiguration } from '../../../client/api-configuration';

@Component({
  standalone: true,
  selector: 'am-upload-widget',
  imports: [FilePondModule],
  template: `
    <file-pond
      #pond
      [options]="pondOptions"
      (onprocessfile)="onProcessed($event)"
    ></file-pond>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UploadWidgetComponent {
  @ViewChild('pond') pond!: FilePondComponent;
  private auth = inject(OAuthService);
  private api = inject(ApiConfiguration);

  pondOptions: FilePondOptions = {
    allowMultiple: true,
    name: 'file',
    labelIdle: 'Dateien hier ablegen oder klicken...',
    server: {
      process: {
        url: `${this.api.rootUrl.replace(/\/$/, '')}/api/documents`,
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${this.auth.getAccessToken()}`,
          'X-Requested-With': 'XMLHttpRequest'
        },
        withCredentials: false,
        ondata: (formData) => {
          formData.append('Language', 'deu');
          return formData;
        },
        onload: () => '',
        onerror: (resp) => resp
      }
    }
  };

  onProcessed(event: any): void {
    if (event?.error) return;
    if (event?.file && this.pond && this.pond['pond']) {
      this.pond['pond'].removeFile(event.file.id);
    }
  }
}
