// ==UserScript==
// @name         Рэдагаванне шматкутніка тэрыторыі
// @namespace    http://tampermonkey.net/
// @version      0.1
// @description  Дазваляе адлюстраваць і адрэдагаваць шматкутнік тэрыторыі для праекту i-walk.by.
// @author       Мікалай Шуркаеў <mshurkaeu.public@gmail.com>
// @match        https://www.openstreetmap.org/*
// @icon         https://www.google.com/s2/favicons?sz=64&domain=openstreetmap.org
// @grant        none
// @run-at       document-body
// ==/UserScript==

(function() {
	'use strict';

	function addMyButtons() {
		let export_tab = document.getElementById('export_tab');
		let showMyRegionButton = document.createElement('a');
		showMyRegionButton.setAttribute('href', '#');
		showMyRegionButton.setAttribute('class', 'btn btn-outline-primary geolink');
		showMyRegionButton.innerText = 'Паказаць тэрыторыю на карце';
		showMyRegionButton.onclick = showMyRegion;
		export_tab.parentNode.insertBefore(showMyRegionButton, export_tab.nextSibling);

		let getMyRegionCoordsButton = document.createElement('a');
		getMyRegionCoordsButton.setAttribute('href', '#');
		getMyRegionCoordsButton.setAttribute('class', 'btn btn-outline-primary geolink');
		getMyRegionCoordsButton.innerText = 'Скапіраваць каардынаты тэрыторыі';
		getMyRegionCoordsButton.onclick = getMyRegionCoords;
		export_tab.parentNode.insertBefore(getMyRegionCoordsButton, showMyRegionButton.nextSibling);
	}

	let myRegion = null;

	function getMyRegionCoords() {
		if (!myRegion) {
			return false;
		}

		let latLngs = myRegion.getLatLngs()[0];
		let urls = [];
		for (let i=0; i<latLngs.length; i++) {
			let latLng = latLngs[i];
			urls.push('"https://www.openstreetmap.org/?mlat=' + latLng.lat + '&mlon=' + latLng.lng + '"');
		}
		let res = urls.join(',\n');
		window.navigator.clipboard.writeText(res);
		return false;
	}

	function newReady(f) {
		let fullCode = f.toString();
		const prefix = 'function(){';
		const postfix= '}';
		let internalCode = fullCode.substring(prefix.length, fullCode.length-1-postfix.length+1);
		let betterCode = internalCode.replace(/=new L\.OSM\.Map\("map"/, '=window._my_map=new L.OSM.Map("map"');
		let betterF = new Function(betterCode);
		return this._original_ready(betterF);
	}

	function showMyRegion(e) {
		let urlsText = prompt('Устаўце сюды тэкст з URL-амі тэрыторыі');
		if (!urlsText) {
			return false;
		}

		if (!window._my_map.editTools) {
			window._my_map.editTools = new window.L.Editable(window._my_map, {});
		}

		if (myRegion) {
			window._my_map.removeLayer(myRegion);
		}

		//напрыклад "https://www.openstreetmap.org/?mlat=53.94425524943648&mlon=27.461211271581885"
		const latPrefix = '?mlat=';
		const lonPrefix = '&mlon=';

		let urls = urlsText.split(',');
		let errors = [];
		let latLngs = [];
		let setView = true;
		for (let i=0; i<urls.length; i++) {
			let url = urls[i];
			let latStart = url.indexOf(latPrefix) + latPrefix.length;
			if (latStart > latPrefix.length) {
				let latEnd = url.indexOf(lonPrefix);
				let lonStart = url.indexOf(lonPrefix) + lonPrefix.length;
				let lonEnd = url.length;
				let latStr = url.substring(latStart, latEnd);
				let lonStr = url.substring(lonStart, lonEnd);
				let lat = parseFloat(latStr);
				let lon = parseFloat(lonStr);
				latLngs.push([lat, lon]);
				if (setView) {
					setView = false;
					window._my_map.setView([lat, lon]);
				}
			}
			else {
				errors.push('нешта незразумелае на радку ' + (i+1) + ':' + url);
			}
		}

		myRegion = window.L.polygon(latLngs, {editorClass: window.L.Editable.PolygonEditor}).addTo(window._my_map);
		myRegion.enableEdit();

		if (errors.length > 0) {
			alert(errors.join('\n'));
		}
		return false;
	}

	let appScript = document.scripts[0];//<script src="/assets/application-92f16e1b2f8ca5a3e46717b2f6879a122b643444fc64938a22ff68bba7ac8adf.js"></script>
	appScript.onload = function() {
		var d = window.$(document);
		d.ready(addMyButtons);

		var proto = Object.getPrototypeOf(d);
		proto._original_ready = proto.ready;
		proto.ready = newReady;
	};
	let leafletEditable = document.createElement('script');
	leafletEditable.setAttribute('src', 'https://leaflet.github.io/Leaflet.Editable/src/Leaflet.Editable.js');
	appScript.parentNode.insertBefore(leafletEditable, appScript.nextSibling);
})();